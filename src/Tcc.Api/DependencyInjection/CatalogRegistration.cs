using Tcc.Api.Catalog.Adapters;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;
using Tcc.Infrastructure.Persistence.Repositories;

namespace Tcc.Api.DependencyInjection;

public static class CatalogRegistration
{
    public static IServiceCollection AddCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        CatalogMemoryOptions options;
        try
        {
            options = configuration.GetSection("IGDB:CatalogMemory").Get<CatalogMemoryOptions>() ?? new();
            options.Validate();
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Configuração de memória do catálogo inválida.");
        }
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
