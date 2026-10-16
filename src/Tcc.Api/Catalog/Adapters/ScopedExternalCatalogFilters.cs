using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Catalog.Adapters;

internal sealed class ScopedExternalCatalogFilters(IServiceScopeFactory scopes) : IExternalCatalogFilters
{
    public async Task<ExternalCatalogFilters> ReadAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var source = new IgdbCatalogFilters(() => scope.ServiceProvider.GetRequiredService<IgdbAuthenticatedClient>());
        try { return await source.ReadAsync(ct); }
        catch (InvalidOperationException) { throw new CatalogIntegrationUnavailableException(false); }
    }
}
