using Tcc.Infrastructure.Igdb;
using Tcc.Application.Catalog;

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
        services.AddSingleton(services =>
        {
            var factory = services.GetRequiredService<IHttpClientFactory>();
            var options = services.GetRequiredService<IgdbCredentialOptions>();
            var clock = services.GetRequiredService<TimeProvider>();
            return new TwitchTokenProvider(async cancellationToken =>
            {
                using var http = factory.CreateClient(OAuthClientName);
                return await new TwitchOAuthClient(http, options, clock).AcquireAsync(cancellationToken);
            }, clock);
        });
        services.AddScoped(services => new IgdbAuthenticatedClient(
            services.GetRequiredService<IHttpClientFactory>().CreateClient(IgdbClientName),
            services.GetRequiredService<IgdbCredentialOptions>(), services.GetRequiredService<TwitchTokenProvider>()));
        services.AddScoped<IExternalGameCatalog>(services => new IgdbGameCatalog(
            () => services.GetRequiredService<IgdbAuthenticatedClient>(), services.GetRequiredService<TimeProvider>()));
        return services;
    }
}
