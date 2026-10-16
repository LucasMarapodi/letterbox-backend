using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Filters;
using Tcc.Application.Catalog;
using Tcc.Application.Opinions;
using Tcc.Infrastructure.Persistence.Repositories;

namespace Tcc.Api.DependencyInjection;

public static class OpinionsRegistration
{
    public static IServiceCollection AddOpinionsFeature(this IServiceCollection services)
    {
        services.AddScoped<QueryGames>();
        services.AddScoped<CatalogUsage>();
        services.AddScoped<CatalogHeadersFilter>();
        services.Configure<MvcOptions>(options => options.Filters.AddService<CatalogHeadersFilter>());
        services.AddScoped<IRatings, PostgresRatings>();
        services.AddScoped<PersonalRating>();
        services.AddScoped<LocalRatingAverage>();
        services.AddScoped<IReviews, PostgresReviews>();
        services.AddScoped<OpinionViews>();
        services.AddScoped<PublishReview>();
        services.AddScoped<GetReview>();
        services.AddScoped<QueryReviews>();
        services.AddScoped<ReviewProjection>();
        services.AddScoped<EditReview>();
        services.AddScoped<IComments, PostgresComments>();
        services.AddScoped<ICommentLikes, PostgresCommentLikes>();
        services.AddScoped<PersonalCommentLike>();
        services.AddScoped<IReviewLikes, PostgresReviewLikes>();
        services.AddScoped<PersonalReviewLike>();
        services.AddScoped<QueryComments>();
        services.AddScoped<PublishComment>();
        return services;
    }
}
