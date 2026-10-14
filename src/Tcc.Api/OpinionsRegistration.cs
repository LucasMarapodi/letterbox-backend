using Tcc.Application.Catalog;

using Tcc.Application.Opinions;
using Tcc.Infrastructure.Opinions;

namespace Tcc.Api;

public static class OpinionsRegistration
{
    public static IServiceCollection AddOpinionsFeature(this IServiceCollection services)
    {

        services.AddScoped<QueryGames>();
        services.AddScoped<CatalogUsage>();
        services.AddScoped<CatalogHeadersFilter>();
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.Filters.AddService<CatalogHeadersFilter>());
        services.AddSingleton<IRatings, InMemoryRatings>();
        services.AddScoped<PersonalRating>();
        services.AddScoped<LocalRatingAverage>();
        services.AddSingleton<IReviews, InMemoryReviews>();
        services.AddScoped<OpinionViews>();
        services.AddScoped<PublishReview>();
        services.AddScoped<GetReview>();
        services.AddScoped<QueryReviews>();
        services.AddScoped<ReviewProjection>();
        services.AddScoped<EditReview>();
        services.AddSingleton<IComments, InMemoryComments>();
        services.AddScoped<QueryComments>();
        services.AddScoped<PublishComment>();
        return services;
    }
}

