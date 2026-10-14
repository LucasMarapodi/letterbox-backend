using Tcc.Domain.Catalog;
using Tcc.Domain.Identity;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;

namespace Tcc.Application.Catalog;

public sealed record CatalogSearch
{
    private CatalogSearch(string term, string sort, int page, int pageSize)
    { Term = term; Sort = sort; Page = page; PageSize = pageSize; }
    public string Term { get; }
    public string Sort { get; }
    public int Page { get; }
    public int PageSize { get; }
    public static CatalogSearch Create(string? term = null, string? sort = null, int page = 1, int pageSize = 20)
    {
        term = term?.Trim() ?? "";
        sort ??= term.Length == 0 ? "release-desc" : "name-asc";
        if (term.Length > 200 || term.Any(char.IsControl) || sort is not ("release-desc" or "name-asc" or "name-desc")
            || page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new DomainValidationException("Consulta de catálogo inválida.");
        return new(term, sort, page, pageSize);
    }
    public int Offset => (Page - 1) * PageSize;
}

public static class CatalogIds
{
    public static long Parse(string id)
    {
        if (id is null || !id.StartsWith("igdb-", StringComparison.Ordinal)
            || !long.TryParse(id.AsSpan(5), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || number <= 0 || id != Format(number)) throw new DomainValidationException("ID de catálogo inválido.");
        return number;
    }
    public static string Format(long number)
    {
        if (number <= 0) throw new DomainValidationException("ID de catálogo inválido.");
        return "igdb-" + number.ToString(CultureInfo.InvariantCulture);
    }
    public static string[] Unique(IReadOnlyList<string> ids)
    {
        var unique = ids.Distinct(StringComparer.Ordinal).ToArray();
        if (unique.Length > 500) throw new DomainValidationException("Lote de catálogo excede 500 IDs.");
        foreach (var id in unique) Parse(id);
        return unique;
    }
}

public sealed record CatalogMetadata(DateTimeOffset FetchedAt, bool IsStale = false);
public sealed record CatalogGame(Game Game, CatalogMetadata Metadata);
public sealed record CatalogPage(CatalogSearch Query, IReadOnlyList<CatalogGame> Items, bool HasNextPage,
    CatalogMetadata Metadata, bool IsComplete = true);

public interface ICatalogStore
{
    Task<CatalogPage?> FindPageAsync(CatalogSearch query, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, CatalogGame>> FindGamesAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);
    Task SavePageAsync(CatalogPage page, CancellationToken cancellationToken);
    Task SaveGamesAsync(IReadOnlyList<CatalogGame> games, CancellationToken cancellationToken);
}
public interface ICatalogCache : ICatalogStore;
public interface ILocalGameCatalog : ICatalogStore;
public interface IExternalGameCatalog
{
    Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
public interface IAsyncGameCatalog
{
    Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken = default);
    Task<CatalogGame?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default);
}

public sealed class AsyncGameCatalog(ICatalogCache cache, ILocalGameCatalog local, IExternalGameCatalog external) : IAsyncGameCatalog
{
    public async Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await cache.FindPageAsync(query, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (Usable(page, query)) return page;
        page = await local.FindPageAsync(query, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Usable(page, query))
        {
            page = await external.SearchAsync(query, cancellationToken);
            if (!Usable(page, query)) throw new CatalogIntegrationUnavailableException();
            await local.SaveGamesAsync(page.Items, cancellationToken);
            await local.SavePageAsync(page, cancellationToken);
        }
        await cache.SaveGamesAsync(page.Items, cancellationToken);
        await cache.SavePageAsync(page, cancellationToken);
        return page;
    }
    public async Task<CatalogGame?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var games = await ResolveAsync([id], cancellationToken);
        return games.GetValueOrDefault(id);
    }
    public async Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unique = CatalogIds.Unique(ids);
        var result = new Dictionary<string, CatalogGame>(StringComparer.Ordinal);
        if (unique.Length == 0) return result;
        var cached = await cache.FindGamesAsync(unique, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        AddFound(result, cached, unique);
        var missing = unique.Where(id => !result.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            var known = await local.FindGamesAsync(missing, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AddFound(result, known, missing);
            if (known.Count > 0) await cache.SaveGamesAsync(known.Values.ToArray(), cancellationToken);
            missing = unique.Where(id => !result.ContainsKey(id)).ToArray();
            if (missing.Length > 0)
            {
                var discovered = await external.ResolveAsync(missing, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                AddFound(result, discovered, missing);
                if (discovered.Count > 0)
                {
                    await local.SaveGamesAsync(discovered.Values.ToArray(), cancellationToken);
                    await cache.SaveGamesAsync(discovered.Values.ToArray(), cancellationToken);
                }
            }
        }
        return unique.Where(result.ContainsKey).ToDictionary(id => id, id => result[id], StringComparer.Ordinal);
    }
    private static bool Usable([NotNullWhen(true)] CatalogPage? page, CatalogSearch query) => page is not null && page.Query == query
        && page.IsComplete && !page.Metadata.IsStale && page.Items.Count <= query.PageSize;
    private static void AddFound(Dictionary<string, CatalogGame> result, IReadOnlyDictionary<string, CatalogGame> source, string[] requested)
    {
        foreach (var id in requested)
            if (source.TryGetValue(id, out var entry) && entry.Game.Id == id) result.Add(id, entry);
    }
}
