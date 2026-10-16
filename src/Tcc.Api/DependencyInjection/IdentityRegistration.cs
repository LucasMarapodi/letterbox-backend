using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Tcc.Api.Authentication;
using Tcc.Application.Identity;
using Tcc.Infrastructure.Identity;
using Tcc.Infrastructure.Persistence.Repositories;
using SessionOptions = Tcc.Api.Authentication.SessionOptions;

namespace Tcc.Api.DependencyInjection;

public static class IdentityRegistration
{
    public static IServiceCollection AddIdentityFeature(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IUserAccounts, PostgresUserAccounts>();
        services.AddScoped<IUserSessions, PostgresUserSessions>();
        services.AddScoped<RegisterAccount>();
        services.AddScoped<LoginAccount>();
        services.AddScoped<SessionAccess>();
        services.AddScoped<CurrentAccount>();
        services.AddScoped<LogoutAccount>();
        services.AddScoped<GetUserProfile>();
        services.AddScoped<EditUserProfile>();
        services.AddScoped<IProfileCompletionStore, PostgresProfileCompletion>();
        services.AddScoped<ProfileCompletion>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentSession, HttpCurrentSession>();
        services.AddAuthentication(SessionBearerHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionBearerHandler>(SessionBearerHandler.SchemeName, _ => { });
        services.AddAuthorization();
        services.AddOptions<SessionOptions>()
            .BindConfiguration("Authentication")
            .Validate(options => options.SessionMinutes > 0, "SessionMinutes deve ser positivo.")
            .ValidateOnStart();
        services.AddSingleton(provider => new SessionSettings(TimeSpan.FromMinutes(
            provider.GetRequiredService<IOptions<SessionOptions>>().Value.SessionMinutes)));
        return services;
    }
}
