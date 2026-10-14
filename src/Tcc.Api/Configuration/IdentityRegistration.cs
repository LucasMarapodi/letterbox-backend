using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Tcc.Api.Authentication;
using Tcc.Api.ErrorHandling;
using SessionOptions = Tcc.Api.Authentication.SessionOptions;
using Tcc.Application.Identity;
using Tcc.Infrastructure.Identity;

namespace Tcc.Api.Configuration;

public static class IdentityRegistration
{
    public static IServiceCollection AddIdentityFeature(this IServiceCollection services)
    {
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IUserAccounts, InMemoryUserAccounts>();
        services.AddSingleton<IMockSessions, InMemoryMockSessions>();
        services.AddScoped<RegisterAccount>();
        services.AddScoped<LoginAccount>();
        services.AddScoped<SessionAccess>();
        services.AddScoped<CurrentAccount>();
        services.AddScoped<LogoutAccount>();
        services.AddScoped<GetUserProfile>();
        services.AddScoped<EditUserProfile>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentSession, HttpCurrentSession>();
        services.AddAuthentication(MockBearerHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, MockBearerHandler>(MockBearerHandler.SchemeName, _ => { });
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
