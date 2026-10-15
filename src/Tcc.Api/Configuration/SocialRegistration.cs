using Tcc.Application.Social;
using Tcc.Infrastructure.Social;

namespace Tcc.Api.Configuration;

public static class SocialRegistration
{
    public static IServiceCollection AddSocialFeature(this IServiceCollection services)
    {
        services.AddScoped<IFollows, Tcc.Infrastructure.Persistence.Repositories.PostgresFollows>();
        services.AddScoped<FollowUsers>();
        services.AddScoped<IFavorites, Tcc.Infrastructure.Persistence.Repositories.PostgresFavorites>();
        services.AddScoped<PersonalFavorites>();
        services.AddSingleton<InMemoryPublicationScope>();
        services.AddSingleton<IPublicationScope>(provider => provider.GetRequiredService<InMemoryPublicationScope>());
        services.AddSingleton<INotifications, InMemoryNotifications>();
        services.AddScoped<PublicationNotifications>();
        services.AddScoped<QueryNotifications>();
        return services;
    }
}
