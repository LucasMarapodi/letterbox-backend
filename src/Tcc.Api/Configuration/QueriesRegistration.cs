using Tcc.Application.Queries;

namespace Tcc.Api.Configuration;

public static class QueriesRegistration
{
    public static IServiceCollection AddQueriesFeature(this IServiceCollection services)
    {
        services.AddScoped<FollowingFeed>();
        services.AddScoped<PersonalDashboard>();
        services.AddScoped<RecentReviews>();
        services.AddScoped<CommentActivity>();
        services.AddScoped<MostCommentedReviews>();
        services.AddScoped<GamesInDiscussion>();
        return services;
    }
}
