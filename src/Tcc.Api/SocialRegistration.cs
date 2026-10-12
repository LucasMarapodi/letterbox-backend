using Tcc.Application.Social;
using Tcc.Infrastructure.Social;

namespace Tcc.Api;

public static class SocialRegistration
{
    public static IServiceCollection AddSocialFeature(this IServiceCollection services)
    {
        services.AddSingleton<IFollows, InMemoryFollows>();
        services.AddScoped<FollowUsers>();
        services.AddSingleton<IFavorites, InMemoryFavorites>();
        services.AddScoped<PersonalFavorites>();
        return services;
    }
}
