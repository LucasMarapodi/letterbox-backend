using Tcc.Infrastructure.Igdb;

namespace Tcc.Api;

public static class IgdbAuthenticationRegistration
{
    public const string OAuthClientName = "TwitchOAuth";
    public const string IgdbClientName = "IgdbExternal";

    public static IServiceCollection AddIgdbAuthentication(this IServiceCollection services)
    {
        services.AddSingleton(_ => IgdbCredentialOptions.Read(name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)));
        services.AddHttpClient(OAuthClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.MaxResponseContentBufferSize = 64 * 1024;
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(ExternalHttpTransport.CreateHandler);
        services.AddHttpClient(IgdbClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.MaxResponseContentBufferSize = 4 * 1024 * 1024;
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(ExternalHttpTransport.CreateHandler);
        services.AddTransient(services => new TwitchOAuthClient(
            services.GetRequiredService<IHttpClientFactory>().CreateClient(OAuthClientName),
            services.GetRequiredService<IgdbCredentialOptions>(), services.GetRequiredService<TimeProvider>()));
        return services;
    }
}
