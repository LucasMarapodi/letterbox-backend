using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Igdb;

public sealed class IgdbGameCatalog(IgdbAuthenticatedClient client, TimeProvider clock) : IExternalGameCatalog
{
    public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken)
    { _ = client; _ = clock; _ = query; _ = cancellationToken; throw new NotImplementedException(); }
    public Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    { _ = ids; _ = cancellationToken; throw new NotImplementedException(); }
}
