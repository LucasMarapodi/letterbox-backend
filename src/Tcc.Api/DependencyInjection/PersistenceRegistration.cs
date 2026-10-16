using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tcc.Api.Configuration;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.DependencyInjection;

public static class PersistenceRegistration
{
    public static IServiceCollection AddPostgresPersistence(this IServiceCollection services, IHostEnvironment environment)
    {
        return services.AddPostgresPersistence(name =>
        {
            Func<string, string?> read = Environment.GetEnvironmentVariable;
            if (environment.IsDevelopment())
                read = LocalBackendEnvironment.CreateReader(LocalBackendEnvironment.PathFor(environment.ContentRootPath), read);
            return read(name);
        });
    }

    public static IServiceCollection AddPostgresPersistence(this IServiceCollection services, Func<string, string?> readVariable)
    {
        services.AddSingleton(_ => DatabaseSettings.Read(readVariable));
        services.AddDbContext<TccDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<DatabaseSettings>().ConnectionString)
            .UseLoggerFactory(NullLoggerFactory.Instance).EnableSensitiveDataLogging(false));
        services.AddScoped(provider => new Lazy<TccDbContext>(() => provider.GetRequiredService<TccDbContext>()));
        return services;
    }
}
