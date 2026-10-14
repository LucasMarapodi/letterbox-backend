using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api;

public static class CatalogMemoryRegistration
{
    public static IServiceCollection AddCatalogMemory(this IServiceCollection services, IConfiguration configuration)
    {
        CatalogMemoryOptions options;
        try { options = configuration.GetSection("IGDB:CatalogMemory").Get<CatalogMemoryOptions>() ?? new(); options.Validate(); }
        catch (Exception) { throw new InvalidOperationException("Configuração de memória do catálogo inválida."); }
        services.AddSingleton(options);
        services.AddSingleton<ICatalogCache, InMemoryCatalogCache>();
        services.AddSingleton<ILocalGameCatalog, InMemoryLocalGameCatalog>();
        services.AddSingleton<IAsyncGameCatalog>(provider => new SingleFlightGameCatalog(new AsyncGameCatalog(
            provider.GetRequiredService<ICatalogCache>(), provider.GetRequiredService<ILocalGameCatalog>(),
            provider.GetRequiredService<IExternalGameCatalog>()), provider.GetRequiredService<CatalogMemoryOptions>()));
        return services;
    }
}

internal sealed class ScopedExternalGameCatalog(IServiceScopeFactory scopes, TimeProvider clock) : IExternalGameCatalog
{
    public async Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var source = new IgdbGameCatalog(() => scope.ServiceProvider.GetRequiredService<IgdbAuthenticatedClient>(), clock);
        return await source.SearchAsync(query, cancellationToken);
    }
    public async Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var source = new IgdbGameCatalog(() => scope.ServiceProvider.GetRequiredService<IgdbAuthenticatedClient>(), clock);
        return await source.ResolveAsync(ids, cancellationToken);
    }
}
