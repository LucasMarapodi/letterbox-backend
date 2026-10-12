using Tcc.Application.Queries;

namespace Tcc.Api;

public static class QueriesRegistration
{
    public static IServiceCollection AddQueriesFeature(this IServiceCollection services)
    {
        services.AddScoped<FollowingFeed>();
        services.AddScoped<PersonalDashboard>();
        return services;
    }
}
