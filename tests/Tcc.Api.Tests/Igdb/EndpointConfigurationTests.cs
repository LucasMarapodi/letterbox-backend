using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tcc.Api.Configuration;
using Tcc.Api.DependencyInjection;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class EndpointConfigurationTests
{
    [Theory]
    [InlineData("IGDB__BaseUrl")]
    [InlineData("IGDB__ImagesBaseUrl")]
    [InlineData("Twitch__TokenEndpoint")]
    public void Local_environment_accepts_endpoint_keys_and_process_value_takes_priority(string name)
    {
        using var file = new EnvironmentFile(name + "=https://file.example/path");
        Assert.Equal("https://file.example/path", LocalBackendEnvironment.CreateReader(file.Path, _ => null)(name));
        Assert.Equal("https://process.example/path", LocalBackendEnvironment.CreateReader(file.Path, _ => "https://process.example/path")(name));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("/", false)]
    [InlineData("///", false)]
    [InlineData("", true)]
    [InlineData("/", true)]
    [InlineData("///", true)]
    public async Task Configured_paths_preserve_prefixes_images_headers_and_refresh_rejected_token(string trailing, bool search)
    {
        var oauth = 0;
        var games = 0;
        await using var host = Host(new Dictionary<string, string?>
        {
            ["IGDB:BaseUrl"] = "https://catalog.example/proxy/v4" + trailing,
            ["IGDB:ImagesBaseUrl"] = "https://covers.example/assets/igdb/image/upload" + trailing,
            ["Twitch:TokenEndpoint"] = "https://identity.example/proxy/oauth2/token"
        }, services =>
        {
            services.AddSingleton(IgdbTest.Options());
            services.AddHttpClient(IgdbAuthenticationRegistration.OAuthClientName).ConfigurePrimaryHttpMessageHandler(() =>
                new TestHandler(async (request, ct) =>
                {
                    Assert.Equal("https://identity.example/proxy/oauth2/token", request.RequestUri?.AbsoluteUri);
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Null(request.Headers.Authorization);
                    Assert.Equal("", request.RequestUri?.Query);
                    var form = await request.Content!.ReadAsStringAsync(ct);
                    Assert.Contains("client_id=synthetic-id", form);
                    Assert.Contains("client_secret=synthetic-secret%26%2B%3D", form);
                    return IgdbTest.TokenResponse("synthetic-token-" + Interlocked.Increment(ref oauth));
                }));
            services.AddHttpClient(IgdbAuthenticationRegistration.IgdbClientName).ConfigurePrimaryHttpMessageHandler(() =>
                new TestHandler((request, _) =>
                {
                    Assert.Equal("https://catalog.example/proxy/v4/games", request.RequestUri?.AbsoluteUri);
                    Assert.Equal("synthetic-id", Assert.Single(request.Headers.GetValues("Client-ID")));
                    Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                    var attempt = Interlocked.Increment(ref games);
                    Assert.Equal("synthetic-token-" + attempt, request.Headers.Authorization?.Parameter);
                    return Task.FromResult(attempt == 1 ? IgdbTest.Json("", HttpStatusCode.Unauthorized) :
                        IgdbTest.Json("""[{"id":3000000001,"name":"Configured game","cover":{"image_id":"co_test","url":"https://ignored.example/cover"}}]"""));
                }));
        });
        using var scope = host.Services.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<IExternalGameCatalog>();
        var result = search ? Assert.Single((await source.SearchAsync(CatalogSearch.Create("Configured"), default)).Items)
            : (await source.ResolveAsync([3_000_000_001], default))[3_000_000_001];
        Assert.Equal("https://covers.example/assets/igdb/image/upload/t_cover_big/co_test.jpg", result.Game.CoverUrl);
        Assert.Equal(0, result.Game.Id);
        Assert.Equal(2, oauth);
        Assert.Equal(2, games);
    }

    public static IEnumerable<object[]> InvalidEndpoints()
    {
        foreach (var name in new[] { "IGDB:BaseUrl", "IGDB:ImagesBaseUrl", "Twitch:TokenEndpoint" })
            foreach (var value in new[] { "", "/relative", "http://supplier.example/path", "ftp://supplier.example/path",
                "https://user:synthetic-sentinel@supplier.example/path", "https://supplier.example/path?synthetic-sentinel=x",
                "https://supplier.example/path#synthetic-sentinel", "not-a-uri-synthetic-sentinel",
                "https://supplier.example/path?", "https://supplier.example/path#", "https://supplier.example/path\\other" })
                yield return [name, value];
    }

    [Theory]
    [MemberData(nameof(InvalidEndpoints))]
    public async Task Invalid_endpoint_fails_at_startup_without_disclosing_the_value(string name, string value)
    {
        await using var host = Host(new Dictionary<string, string?> { [name] = value });
        var failure = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("Configuração de URLs externas inválida", failure.Message);
        Assert.DoesNotContain("synthetic-sentinel", failure.ToString());
        Assert.Null(failure.InnerException);
    }

    [Theory]
    [InlineData("oauth", 301)]
    [InlineData("oauth", 307)]
    [InlineData("igdb", 302)]
    [InlineData("igdb", 308)]
    public async Task Configured_supplier_redirect_is_rejected_without_following_location(string stage, int status)
    {
        var calls = 0;
        using var transport = ExternalHttpTransport.CreateHandler();
        Assert.False(transport.AllowAutoRedirect);
        Assert.False(transport.UseCookies);
        await using var host = Host(new Dictionary<string, string?>
        {
            ["IGDB:BaseUrl"] = "https://catalog.example/v4/",
            ["Twitch:TokenEndpoint"] = "https://identity.example/oauth2/token"
        }, services =>
        {
            services.AddSingleton(IgdbTest.Options());
            foreach (var (name, current) in new[] { (IgdbAuthenticationRegistration.OAuthClientName, "oauth"), (IgdbAuthenticationRegistration.IgdbClientName, "igdb") })
                services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => new TestHandler((request, _) =>
                {
                    Assert.NotEqual("redirect.example", request.RequestUri?.Host);
                    Interlocked.Increment(ref calls);
                    if (current != stage) return Task.FromResult(IgdbTest.TokenResponse());
                    var response = IgdbTest.Json("", (HttpStatusCode)status);
                    response.Headers.Location = new Uri("https://redirect.example/synthetic-sentinel");
                    return Task.FromResult(response);
                }));
        });
        using var scope = host.Services.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<IExternalGameCatalog>();
        var failure = await Record.ExceptionAsync(() => source.ResolveAsync([1], default));
        Assert.True(failure is ExternalIntegrationUnavailableException or CatalogInvalidResponseException);
        Assert.DoesNotContain("synthetic-sentinel", failure.ToString());
        Assert.Equal(stage == "oauth" ? 1 : 2, calls);
    }

    private static WebApplicationFactory<Program> Host(Dictionary<string, string?> values, Action<IServiceCollection>? configure = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            if (configure is not null) builder.ConfigureServices(configure);
        });
}
