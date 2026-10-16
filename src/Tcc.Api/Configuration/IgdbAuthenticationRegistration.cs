using Tcc.Infrastructure.Igdb;
using Tcc.Application.Catalog;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tcc.Api.Configuration;

public static class IgdbAuthenticationRegistration
{
    public const string OAuthClientName = "TwitchOAuth";
    public const string IgdbClientName = "IgdbExternal";

    public static IServiceCollection AddIgdbAuthentication(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(provider =>
        {
            var environment = provider.GetRequiredService<IHostEnvironment>();
            Func<string, string?> read = Environment.GetEnvironmentVariable;
            if (environment.IsDevelopment())
                read = LocalBackendEnvironment.CreateReader(LocalBackendEnvironment.PathFor(environment.ContentRootPath), read);
            return ExternalEndpointOptions.Read(name => read(name) ?? configuration?[name.Replace("__", ":", StringComparison.Ordinal)]);
        });
        services.AddSingleton<IgdbGameMapper>();
        IgdbResilienceOptions resilience;
        try { resilience = configuration?.GetSection("IGDB:Resilience").Get<IgdbResilienceOptions>() ?? new(); resilience.Validate(); }
        catch (Exception) { throw new InvalidOperationException("Configuração de resiliência IGDB inválida."); }
        services.AddSingleton(resilience);
        services.AddSingleton<IIgdbScheduler, SystemIgdbScheduler>();
        services.AddSingleton<IgdbResiliencePolicy>();
        services.AddSingleton(provider =>
        {
            var environment = provider.GetRequiredService<IHostEnvironment>();
            Func<string, string?> read = name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
            if (environment.IsDevelopment())
                read = LocalBackendEnvironment.CreateReader(LocalBackendEnvironment.PathFor(environment.ContentRootPath), read);
            return IgdbCredentialOptions.Read(read);
        });
        services.AddHttpClient(OAuthClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.MaxResponseContentBufferSize = 64 * 1024;
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(ExternalHttpTransport.CreateHandler);
        services.AddHttpClient(IgdbClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
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
                return await new TwitchOAuthClient(http, options, clock,
                    services.GetRequiredService<ExternalEndpointOptions>()).AcquireAsync(cancellationToken);
            }, clock);
        });
        services.AddScoped(services => new IgdbAuthenticatedClient(
            services.GetRequiredService<IHttpClientFactory>().CreateClient(IgdbClientName),
            services.GetRequiredService<IgdbCredentialOptions>(), services.GetRequiredService<TwitchTokenProvider>(),
            services.GetRequiredService<IgdbResiliencePolicy>(), services.GetRequiredService<ExternalEndpointOptions>()));
        services.AddSingleton<IExternalGameCatalog, ScopedExternalGameCatalog>();
        return services;
    }
}
