using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Application;
using Tcc.Infrastructure.Persistence.Repositories;

namespace Tcc.Api.DependencyInjection;

public static class QueriesRegistration
{
    public static IServiceCollection AddQueriesFeature(this IServiceCollection services)
    {
        services.AddScoped<FollowingFeed>();
        services.AddScoped<IPeopleSearch, PostgresPeopleSearch>();
        services.AddScoped<AggregatedSearch>();
        services.AddScoped<PersonalDashboard>();
        services.AddScoped<RecentReviews>();
        services.AddScoped<CommentActivity>();
        services.AddScoped<MostCommentedReviews>();
        services.AddScoped<GamesInDiscussion>();
        services.AddScoped<ICommentRanking, PostgresCommentRanking>();
        services.AddScoped<MostLikedComments>();
        services.AddScoped<IReviewRanking, PostgresReviewRanking>();
        services.AddScoped<MostLikedReviews>();
        return services;
    }
}
