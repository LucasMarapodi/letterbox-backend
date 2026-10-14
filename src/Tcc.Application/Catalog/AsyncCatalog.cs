using Tcc.Domain.Catalog;

namespace Tcc.Application.Catalog;

public sealed record CatalogSearch(string Term, string Sort, int Page, int PageSize)
{
    public static CatalogSearch Create(string? term = null, string? sort = null, int page = 1, int pageSize = 20) =>
        new(term?.Trim() ?? "", sort ?? (string.IsNullOrWhiteSpace(term) ? "release-desc" : "name-asc"), page, pageSize);
    public int Offset => (Page - 1) * PageSize;
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
    public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken = default)
    { _ = cache; _ = local; _ = external; _ = query; _ = cancellationToken; throw new NotImplementedException(); }
    public Task<CatalogGame?> GetAsync(string id, CancellationToken cancellationToken = default)
    { _ = id; _ = cancellationToken; throw new NotImplementedException(); }
    public Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    { _ = ids; _ = cancellationToken; throw new NotImplementedException(); }
}
