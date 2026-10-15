using Microsoft.Extensions.DependencyInjection;
using Tcc.Domain.Social;
using Tcc.Infrastructure.Social;
namespace Tcc.Application.Social;

public static class TestSocialAccess
{
    public static IServiceCollection AddTestSocial(this IServiceCollection services)
    {
        services.AddSingleton<IFollows, InMemoryFollows>();
        services.AddSingleton<IFavorites, InMemoryFavorites>();
        return services;
    }
    public static IReadOnlyList<Favorite> ForUser(this IFavorites store, int userId) => ((InMemoryFavorites)store).ForUser(userId);
}
