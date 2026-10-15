using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Configuration;

public static class PersistenceRegistration
{
    public static IServiceCollection AddPostgresPersistence(this IServiceCollection services, Func<string, string?> readVariable)
    {
        services.AddSingleton(_ => DatabaseSettings.Read(readVariable));
        services.AddDbContext<TccDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<DatabaseSettings>().ConnectionString)
            .UseLoggerFactory(NullLoggerFactory.Instance).EnableSensitiveDataLogging(false));
        return services;
    }
}
