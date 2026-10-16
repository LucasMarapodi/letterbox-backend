using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;
using Tcc.Infrastructure.Persistence.Repositories;

namespace Tcc.Api.Configuration;

public static class CatalogMemoryRegistration
{
    public static IServiceCollection AddCatalogMemory(this IServiceCollection services, IConfiguration configuration)
    {
        CatalogMemoryOptions options;
        try { options = configuration.GetSection("IGDB:CatalogMemory").Get<CatalogMemoryOptions>() ?? new(); options.Validate(); }
        catch (Exception) { throw new InvalidOperationException("Configuração de memória do catálogo inválida."); }
        services.AddSingleton(options);
        services.AddSingleton<ICatalogCache, InMemoryCatalogCache>();
        services.AddSingleton<CatalogQuerySnapshots>();
        services.AddScoped<PostgresGameCatalog>();
        services.AddSingleton<ILocalGameCatalog, ScopedSqlLocalGameCatalog>();
        services.AddSingleton<ICatalogFilterStore, ScopedSqlLocalGameCatalog>();
        services.AddSingleton<CatalogFilters>();
        services.AddSingleton<IExternalCatalogFilters, ScopedExternalCatalogFilters>();
        services.AddScoped<DiscoverGame>();
        services.AddSingleton<IAsyncGameCatalog>(provider => new SingleFlightGameCatalog(new AsyncGameCatalog(
            provider.GetRequiredService<ICatalogCache>(), provider.GetRequiredService<ILocalGameCatalog>(),
            provider.GetRequiredService<IExternalGameCatalog>()), provider.GetRequiredService<CatalogMemoryOptions>()));
        return services;
    }
}

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
