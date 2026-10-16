using Tcc.Application.Catalog;
using Tcc.Api.Filters;

using Tcc.Application.Opinions;
using Tcc.Infrastructure.Opinions;

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
        services.AddSingleton<IComments, InMemoryComments>();
        services.AddScoped<QueryComments>();
        services.AddScoped<PublishComment>();
        return services;
    }
}
