namespace Tcc.Api.Configuration;

public static class PersistenceRegistration
{
    public static IServiceCollection AddPostgresPersistence(this IServiceCollection services, Func<string, string?> readVariable) => services;
}
