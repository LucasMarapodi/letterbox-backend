using Tcc.Application.Catalog;
using Tcc.Infrastructure.Catalog;

namespace Tcc.Api;

public static class OpinionsRegistration
{
    public static IServiceCollection AddOpinionsFeature(this IServiceCollection services)
    {
        services.AddSingleton<IGameCatalog, FakeIgdbCatalog>();
        services.AddScoped<QueryGames>();
        return services;
    }
}
