using Tcc.Application.Catalog;
using Tcc.Api.Filters;

using Tcc.Application.Opinions;


namespace Tcc.Api.Configuration;

public static class OpinionsRegistration
{
    public static IServiceCollection AddOpinionsFeature(this IServiceCollection services)
    {

        services.AddScoped<QueryGames>();
        services.AddScoped<CatalogUsage>();
        services.AddScoped<CatalogHeadersFilter>();
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.Filters.AddService<CatalogHeadersFilter>());
        services.AddScoped<IRatings, Tcc.Infrastructure.Persistence.Repositories.PostgresRatings>();
        services.AddScoped<PersonalRating>();
        services.AddScoped<LocalRatingAverage>();
        services.AddScoped<IReviews, Tcc.Infrastructure.Persistence.Repositories.PostgresReviews>();
        services.AddScoped<OpinionViews>();
        services.AddScoped<PublishReview>();
        services.AddScoped<GetReview>();
        services.AddScoped<QueryReviews>();
        services.AddScoped<ReviewProjection>();
        services.AddScoped<EditReview>();
        services.AddScoped<IComments, Tcc.Infrastructure.Persistence.Repositories.PostgresComments>();
        services.AddScoped<ICommentLikes, Tcc.Infrastructure.Persistence.Repositories.PostgresCommentLikes>();
        services.AddScoped<PersonalCommentLike>();
        services.AddScoped<IReviewLikes, Tcc.Infrastructure.Persistence.Repositories.PostgresReviewLikes>();
        services.AddScoped<PersonalReviewLike>();
        services.AddScoped<QueryComments>();
        services.AddScoped<PublishComment>();
        return services;
    }
}
