using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Igdb;

public sealed class SingleFlightGameCatalog(IAsyncGameCatalog inner, CatalogMemoryOptions options) : IAsyncGameCatalog
{
    public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken = default)
    { _ = options; return inner.SearchAsync(query, cancellationToken); }
    public Task<CatalogGame?> GetAsync(string id, CancellationToken cancellationToken = default) => inner.GetAsync(id, cancellationToken);
    public Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default) => inner.ResolveAsync(ids, cancellationToken);
}
