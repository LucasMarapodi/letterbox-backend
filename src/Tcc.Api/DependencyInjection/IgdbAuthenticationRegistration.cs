using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcc.Api.Catalog.Adapters;
using Tcc.Api.Configuration;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.DependencyInjection;

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
        try
        {
            resilience = configuration?.GetSection("IGDB:Resilience").Get<IgdbResilienceOptions>() ?? new();
            resilience.Validate();
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Configuração de resiliência IGDB inválida.");
        }
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
        services.AddSingleton(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var options = provider.GetRequiredService<IgdbCredentialOptions>();
            var clock = provider.GetRequiredService<TimeProvider>();
            return new TwitchTokenProvider(async cancellationToken =>
            {
                using var http = factory.CreateClient(OAuthClientName);
                return await new TwitchOAuthClient(http, options, clock,
                    provider.GetRequiredService<ExternalEndpointOptions>()).AcquireAsync(cancellationToken);
            }, clock);
        });
        services.AddScoped(provider => new IgdbAuthenticatedClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(IgdbClientName),
            provider.GetRequiredService<IgdbCredentialOptions>(), provider.GetRequiredService<TwitchTokenProvider>(),
            provider.GetRequiredService<IgdbResiliencePolicy>(), provider.GetRequiredService<ExternalEndpointOptions>()));
        services.AddSingleton<IExternalGameCatalog, ScopedExternalGameCatalog>();
        return services;
    }
}
