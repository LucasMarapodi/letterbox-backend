namespace Tcc.Api;

public static class IgdbAuthenticationRegistration
{
    public const string OAuthClientName = "TwitchOAuth";
    public const string IgdbClientName = "IgdbExternal";

    public static IServiceCollection AddIgdbAuthentication(this IServiceCollection services) => services;
}
