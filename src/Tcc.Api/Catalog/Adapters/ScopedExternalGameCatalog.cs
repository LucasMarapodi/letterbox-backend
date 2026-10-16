using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Catalog.Adapters;

internal sealed class ScopedExternalGameCatalog(IServiceScopeFactory scopes, TimeProvider clock, IgdbGameMapper mapper) : IExternalGameCatalog
{
    public async Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ILocalGameCatalog>();
        var source = new IgdbGameCatalog(() => scope.ServiceProvider.GetRequiredService<IgdbAuthenticatedClient>(), clock, mapper,
            scope.ServiceProvider.GetRequiredService<ICatalogFilterStore>(), store as ICatalogReleasePages);
        try { return await source.SearchAsync(query, cancellationToken); }
        catch (InvalidOperationException) { throw new ExternalIntegrationUnavailableException(false); }
    }
    public async Task<IReadOnlyDictionary<long, CatalogGame>> ResolveAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var source = new IgdbGameCatalog(() => scope.ServiceProvider.GetRequiredService<IgdbAuthenticatedClient>(), clock, mapper);
        try { return await source.ResolveAsync(ids, cancellationToken); }
        catch (InvalidOperationException) { throw new ExternalIntegrationUnavailableException(false); }
    }
}
