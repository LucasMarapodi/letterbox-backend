using Tcc.Domain.Catalog;
using Tcc.Domain.Identity;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;

namespace Tcc.Application.Catalog;

public sealed record CatalogSearch
{
    private CatalogSearch(string term, string sort, int page, int pageSize, int? genreId, int? platformId)
    { Term = term; Sort = sort; Page = page; PageSize = pageSize; GenreId = genreId; PlatformId = platformId; }
    public string Term { get; }
    public string Sort { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int? GenreId { get; }
    public int? PlatformId { get; }
    public static CatalogSearch Create(string? term = null, string? sort = null, int page = 1, int pageSize = 20, int? genreId = null, int? platformId = null)
    {
        term = term?.Trim() ?? "";
        sort ??= term.Length == 0 ? "release-desc" : "name-asc";
        if (term.Length > 200 || term.Any(char.IsControl) || sort is not ("release-desc" or "name-asc" or "name-desc")
            || page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new DomainValidationException("Consulta de catálogo inválida.");
        if (genreId is <= 0 || platformId is <= 0) throw new InvalidCatalogFilterException();
        return new(term, sort, page, pageSize, genreId, platformId);
    }
    public int Offset => (Page - 1) * PageSize;
}

public static class CatalogIds
{
    public static int Parse(int id)
    { if (id <= 0) throw new DomainValidationException("ID local de jogo inválido."); return id; }
    public static int[] Unique(IReadOnlyList<int> ids)
    {
        var unique = ids.Distinct().ToArray();
        if (unique.Length > 500) throw new DomainValidationException("Lote de catálogo excede 500 IDs.");
        foreach (var id in unique) Parse(id);
        return unique;
    }
}
public static class ExternalCatalogIds
{
    public static long[] Unique(IReadOnlyList<long> ids)
    {
        var unique = ids.Distinct().ToArray();
        if (unique.Length > 500 || unique.Any(id => id <= 0)) throw new DomainValidationException("IDs IGDB inválidos.");
        return unique;
    }
}
public sealed record CatalogMetadata(DateTimeOffset FetchedAt, bool IsStale = false);
public sealed record CatalogGame(Game Game, CatalogMetadata Metadata);
public sealed record CatalogPage(CatalogSearch Query, IReadOnlyList<CatalogGame> Items, bool HasNextPage,
    CatalogMetadata Metadata, bool IsComplete = true, bool AlreadyPersisted = false);
public interface ICatalogStore
{
    Task<CatalogPage?> FindPageAsync(CatalogSearch query, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<int, CatalogGame>> FindGamesAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken);
    Task SavePageAsync(CatalogPage page, CancellationToken cancellationToken);
    Task SaveGamesAsync(IReadOnlyList<CatalogGame> games, CancellationToken cancellationToken);
}
public interface ICatalogCache : ICatalogStore
{
    Task InvalidateAsync(CancellationToken ct) => Task.CompletedTask;
}
public interface ILocalGameCatalog : ICatalogStore
{
    async Task<IReadOnlyList<CatalogGame>> UpsertAsync(IReadOnlyList<CatalogGame> games, CancellationToken cancellationToken)
    { await SaveGamesAsync(games, cancellationToken); return games; }
    Task<IReadOnlyDictionary<long, CatalogGame>> FindByIgdbIdsAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<long, CatalogGame>>(new Dictionary<long, CatalogGame>());
}
public interface IExternalGameCatalog
{
    Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<long, CatalogGame>> ResolveAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken);
}
public interface IAsyncGameCatalog
{
    Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken = default);
    Task<CatalogGame?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<CatalogGame?> RefreshAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, CatalogGame>> ResolveAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default);
}
public interface IKnownCatalogReader
{
    Task<CatalogPage?> FindKnownPageAsync(CatalogSearch query, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<int, CatalogGame>> FindKnownGamesAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken);
}
public sealed class DiscoverGame(ILocalGameCatalog local, IExternalGameCatalog external, ICatalogCache cache)
{
    public async Task<CatalogGame?> Execute(long igdbId, CancellationToken ct = default)
    {
        ExternalCatalogIds.Unique([igdbId]);
        var known = await local.FindByIgdbIdsAsync([igdbId], ct);
        if (known.TryGetValue(igdbId, out var entry)) { await cache.SaveGamesAsync([entry], ct); return entry; }
        var found = await external.ResolveAsync([igdbId], ct);
        if (!found.TryGetValue(igdbId, out var game)) return null;
        var saved = await local.UpsertAsync([game], ct);
        await cache.InvalidateAsync(ct);
        await cache.SaveGamesAsync(saved, ct);
        return saved.Single();
    }
}
public sealed class AsyncGameCatalog(ICatalogCache cache, ILocalGameCatalog local, IExternalGameCatalog external) : IAsyncGameCatalog, IKnownCatalogReader
{
    public async Task<CatalogPage?> FindKnownPageAsync(CatalogSearch query, CancellationToken ct)
    {
        var page = await cache.FindPageAsync(query, ct);
        if (Compatible(page, query)) return page;
        page = await local.FindPageAsync(query, ct);
        if (!Compatible(page, query)) return null;
        if (!page.Metadata.IsStale) { await cache.SaveGamesAsync(page.Items, ct); await cache.SavePageAsync(page, ct); }
        return page;
    }
    public Task<IReadOnlyDictionary<int, CatalogGame>> FindKnownGamesAsync(IReadOnlyList<int> ids, CancellationToken ct) => ResolveAsync(ids, ct);
    public async Task<CatalogGame?> RefreshAsync(int id, CancellationToken ct = default)
    {
        CatalogIds.Parse(id);
        var found = await local.FindGamesAsync([id], ct);
        if (!found.TryGetValue(id, out var known)) return null;
        IReadOnlyDictionary<long, CatalogGame> refreshed;
        try { refreshed = await external.ResolveAsync([known.Game.IgdbId], ct); }
        catch (Exception failure) when (CanFallback(failure)) { return known; }
        if (!refreshed.TryGetValue(known.Game.IgdbId, out var item)) return null;
        var saved = await local.UpsertAsync([item], ct);
        await cache.InvalidateAsync(ct);
        await cache.SaveGamesAsync(saved, ct);
        return saved.Single();
    }
    public async Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var page = await cache.FindPageAsync(query, ct);
        if (Usable(page, query)) return page;
        page = await local.FindPageAsync(query, ct);
        if (!Usable(page, query))
        {
            try { page = await external.SearchAsync(query, ct); }
            catch (Exception failure) when (CanFallback(failure))
            {
                var fallback = await cache.FindPageAsync(query, ct);
                if (Compatible(fallback, query)) return fallback;
                fallback = await local.FindPageAsync(query, ct);
                if (Compatible(fallback, query)) return fallback;
                throw;
            }
            if (!Usable(page, query)) throw new CatalogIntegrationUnavailableException();
            var persisted = page.AlreadyPersisted ? page.Items : await local.UpsertAsync(page.Items, ct);
            if (page.Items.Count > 0) await cache.InvalidateAsync(ct);
            var metadata = new CatalogMetadata(persisted.Select(item => item.Metadata.FetchedAt).Append(page.Metadata.FetchedAt).Min(),
                page.Metadata.IsStale || persisted.Any(item => item.Metadata.IsStale));
            if (!ReferenceEquals(persisted, page.Items) || metadata != page.Metadata) page = page with { Items = persisted, Metadata = metadata };
            await local.SavePageAsync(page, ct);
        }
        await cache.SaveGamesAsync(page.Items, ct);
        await cache.SavePageAsync(page, ct);
        return page;
    }
    public async Task<CatalogGame?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        (await ResolveAsync([id], cancellationToken)).GetValueOrDefault(id);
    public async Task<IReadOnlyDictionary<int, CatalogGame>> ResolveAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var unique = CatalogIds.Unique(ids);
        var result = new Dictionary<int, CatalogGame>();
        if (unique.Length == 0) return result;
        AddFound(result, await cache.FindGamesAsync(unique, ct), unique);
        var missing = unique.Where(id => !result.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            var found = await local.FindGamesAsync(missing, ct);
            ct.ThrowIfCancellationRequested();
            AddFound(result, found, missing);
            if (found.Count > 0) await cache.SaveGamesAsync(found.Values.ToArray(), ct);
        }
        return unique.Where(result.ContainsKey).ToDictionary(id => id, id => result[id]);
    }
    private static bool CanFallback(Exception failure) => failure is CatalogTimeoutException or CatalogIntegrationUnavailableException { AllowsStaleFallback: true };
    private static bool Usable([NotNullWhen(true)] CatalogPage? page, CatalogSearch query) => Compatible(page, query) && !page.Metadata.IsStale;
    private static bool Compatible([NotNullWhen(true)] CatalogPage? page, CatalogSearch query) => page is not null && page.Query == query && page.IsComplete && page.Items.Count <= query.PageSize;
    private static void AddFound(Dictionary<int, CatalogGame> result, IReadOnlyDictionary<int, CatalogGame> source, int[] ids)
    { foreach (var id in ids) if (source.TryGetValue(id, out var entry) && entry.Game.Id == id) result.Add(id, entry); }
}
