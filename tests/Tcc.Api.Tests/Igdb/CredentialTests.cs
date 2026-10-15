using Tcc.Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class CredentialTests
{
    [Fact]
    public void Reads_only_named_variables_and_preserves_credentials_without_printing_them()
    {
        var keys = new List<string>();
        var options = IgdbCredentialOptions.Read(key =>
        {
            keys.Add(key);
            return key switch
            {
                "IGDB__ClientId" => "synthetic-id",
                "IGDB__ClientSecret" => "synthetic-secret&+=",
                "IGDB__TokenRefreshMarginSeconds" => "15",
                _ => throw new InvalidOperationException("Unexpected configuration lookup")
            };
        });
        Assert.Equal("synthetic-id", options.ClientId);
        Assert.Equal("synthetic-secret&+=", options.ClientSecret);
        Assert.Equal(TimeSpan.FromSeconds(15), options.RefreshMargin);
        Assert.Equal(new[] { "IGDB__ClientId", "IGDB__ClientSecret", "IGDB__TokenRefreshMarginSeconds" }, keys);
        Assert.DoesNotContain("synthetic", options.ToString());
    }

    [Theory]
    [InlineData(null, "synthetic-secret", "30")]
    [InlineData("synthetic-id", null, "30")]
    [InlineData(" ", "synthetic-secret", "30")]
    [InlineData("synthetic-id", "synthetic-secret", "-1")]
    [InlineData("synthetic-id", "synthetic-secret", "secret-invalid-margin")]
    public void Invalid_configuration_has_safe_actionable_error(string? id, string? secret, string margin)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => IgdbCredentialOptions.Read(key => key switch
        {
            "IGDB__ClientId" => id,
            "IGDB__ClientSecret" => secret,
            _ => margin
        }));
        Assert.Contains("IGDB__", failure.Message);
        Assert.DoesNotContain("synthetic", failure.ToString());
        Assert.DoesNotContain("secret-invalid-margin", failure.ToString());
    }

    [Fact]
    public void External_transport_disables_redirects_cookies_and_os_credentials_preserving_TLS_validation()
    {
        using var handler = ExternalHttpTransport.CreateHandler();
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
        Assert.False(handler.UseDefaultCredentials);
        Assert.Null(handler.Credentials);
        Assert.Null(handler.ServerCertificateCustomValidationCallback);
    }

    [Fact]
    public async Task Health_and_OpenAPI_do_not_read_credentials_or_acquire_an_external_token()
    {
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(testCatalogBuilder => testCatalogBuilder.ConfigureServices(testCatalogServices => Tcc.Api.Tests.Fixtures.TestCatalogRegistration.AddTestCatalog(testCatalogServices))).WithWebHostBuilder(identityBuilder => identityBuilder.ConfigureServices(identityServices => Tcc.Api.Tests.Fixtures.TestIdentityRegistration.AddTestIdentity(identityServices))).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IgdbCredentialOptions>(_ =>
                throw new InvalidOperationException("Credentials must remain lazy"))));
        using var client = host.CreateClient();
        foreach (var path in new[] { "/health", "/openapi/v1.json" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(200, (int)response.StatusCode);
            var payload = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("ClientSecret", payload);
            Assert.DoesNotContain("IGDB__", payload);
            Assert.DoesNotContain("oauth2/token", payload);
        }
        using var tokenEndpoint = await client.GetAsync("/api/v1/igdb/token");
        Assert.Equal(404, (int)tokenEndpoint.StatusCode);
    }

    [Fact]
    public async Task Known_catalog_reads_do_not_activate_external_authentication_or_transport()
    {
        var calls = 0;
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(testCatalogBuilder => testCatalogBuilder.ConfigureServices(testCatalogServices => Tcc.Api.Tests.Fixtures.TestCatalogRegistration.AddTestCatalog(testCatalogServices))).WithWebHostBuilder(identityBuilder => identityBuilder.ConfigureServices(identityServices => Tcc.Api.Tests.Fixtures.TestIdentityRegistration.AddTestIdentity(identityServices))).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IgdbCredentialOptions>(_ => throw new InvalidOperationException("No credentials on local lookup"));
                foreach (var name in new[] { IgdbAuthenticationRegistration.OAuthClientName, IgdbAuthenticationRegistration.IgdbClientName })
                    services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => new TestHandler((_, _) =>
                    {
                        Interlocked.Increment(ref calls);
                        throw new HttpRequestException("Unexpected external request");
                    }));
            }));
        using var client = host.CreateClient();
        var local = host.Services.GetRequiredService<Tcc.Application.Catalog.ILocalGameCatalog>();
        var metadata = new Tcc.Application.Catalog.CatalogMetadata(DateTimeOffset.UtcNow);
        var game = CatalogFlow.Game(1) with { Metadata = metadata };
        await local.SaveGamesAsync([game], default);
        await local.SavePageAsync(new(Tcc.Application.Catalog.CatalogSearch.Create(), [game], false, metadata), default);
        host.Services.GetRequiredService<Tcc.Application.Opinions.IReviews>().TryAdd(Tcc.Domain.Opinions.Review.Create(
            "known", 1, 1, "Known real catalog game", metadata.FetchedAt));
        foreach (var path in new[] { "/api/v1/games", "/api/v1/games/1", "/api/v1/games/1/opinions", "/api/v1/reviews/known" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(200, (int)response.StatusCode);
        }
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(IgdbAuthenticationRegistration.OAuthClientName)]
    [InlineData(IgdbAuthenticationRegistration.IgdbClientName)]
    public async Task External_clients_do_not_emit_HTTP_logs_even_at_Trace(string name)
    {
        using var logs = new CapturedLogs();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddHttpClient();
        services.AddIgdbAuthentication();
        services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => new TestHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK))));
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(name);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token");
        request.Content = new StringContent("synthetic-secret-body");
        request.Headers.Add("Authorization", "Bearer synthetic-token");
        using var response = await client.SendAsync(request);
        Assert.Empty(logs.ExternalMessages);
    }
}

internal sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
}

internal sealed class CapturedLogs : ILoggerProvider
{
    public System.Collections.Concurrent.ConcurrentQueue<string> ExternalMessages { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Capture(categoryName, ExternalMessages);
    public void Dispose() { }
    private sealed class Capture(string category, System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (category.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal)) messages.Enqueue(formatter(state, exception));
        }
    }
}
