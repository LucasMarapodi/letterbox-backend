using Tcc.Application.Social;
using Tcc.Infrastructure.Persistence.Repositories;

namespace Tcc.Api.DependencyInjection;

public static class SocialRegistration
{
    public static IServiceCollection AddSocialFeature(this IServiceCollection services)
    {
        services.AddScoped<IFollows, PostgresFollows>();
        services.AddScoped<FollowUsers>();
        services.AddScoped<IFavorites, PostgresFavorites>();
        services.AddScoped<PersonalFavorites>();
        services.AddScoped<INotifications, PostgresNotifications>();
        services.AddScoped<PublicationNotifications>();
        services.AddScoped<QueryNotifications>();
        return services;
    }
}
