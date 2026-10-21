using Tcc.Application.Forum;
using Tcc.Infrastructure.Persistence.Repositories;

namespace Tcc.Api.DependencyInjection;

public static class ForumRegistration
{
    public static IServiceCollection AddForumFeature(this IServiceCollection services)
    {
        services.AddScoped<IForum, PostgresForum>();
        services.AddScoped<ForumQueries>();
        return services;
    }
}
