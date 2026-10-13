using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tcc.Api.Tests.Validation;

public sealed class CorsTests
{
    private const string Origin = "http://localhost:5173";

    [Theory]
    [InlineData("/health", 200)]
    [InlineData("/openapi/v1.json", 200)]
    [InlineData("/api/v1/games", 200)]
    [InlineData("/api/v1/games?sort=invalid", 400)]
    [InlineData("/api/v1/auth/me", 401)]
    [InlineData("/api/v1/games/missing", 404)]
    [InlineData("/__tests/failure/403", 403)]
    [InlineData("/__tests/failure/409", 409)]
    [InlineData("/__tests/failure/500", 500)]
    public async Task Allowed_origin_can_read_success_and_error_responses(string path, int status)
    {
        await using var host = new CorsFactory(Origin);
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Origin", Origin);
        using var response = await client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(Origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("Location", response.Headers.GetValues("Access-Control-Expose-Headers"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/register")]
    [InlineData("PATCH", "/api/v1/users/me/profile")]
    [InlineData("PUT", "/api/v1/games/game-1/rating/me")]
    [InlineData("DELETE", "/api/v1/favorites/game/game-1")]
    public async Task Preflight_accepts_json_and_bearer_without_a_session(string method, string path)
    {
        await using var host = new CorsFactory(Origin);
        using var client = host.CreateClient();
        using var request = Preflight(Origin, method, path);
        using var response = await client.SendAsync(request);
        Assert.Equal(204, (int)response.StatusCode);
        Assert.Equal(Origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains(method, response.Headers.GetValues("Access-Control-Allow-Methods").Single().Split(','));
        var headers = response.Headers.GetValues("Access-Control-Allow-Headers").Single().Split(',');
        Assert.Contains(headers, header => header.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headers, header => header.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("http://127.0.0.1:5173")]
    [InlineData("http://localhost:5174")]
    [InlineData("https://untrusted.example")]
    public async Task Unconfigured_origin_receives_no_release_headers(string origin)
    {
        await using var host = new CorsFactory(Origin);
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        using var preflight = Preflight(origin, "PUT", "/api/v1/games/game-1/rating/me");
        using var options = await client.SendAsync(preflight);
        Assert.False(options.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(options.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Configured_origin_replaces_default_and_location_is_visible()
    {
        const string custom = "https://web.example";
        await using var host = new CorsFactory(custom);
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register");
        request.Headers.Add("Origin", custom);
        request.Content = new StringContent("{\"name\":\"CORS\",\"email\":\"cors@example.com\",\"password\":\"p\"}");
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(201, (int)response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(custom, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("Location", response.Headers.GetValues("Access-Control-Expose-Headers"));
        using var rejected = Preflight(Origin, "POST", "/api/v1/auth/register");
        using var options = await client.SendAsync(rejected);
        Assert.False(options.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Production_without_origins_does_not_release_browser_access()
    {
        await using var host = new CorsFactory();
        using var client = host.CreateClient();
        using var request = Preflight(Origin, "POST", "/api/v1/auth/login");
        using var response = await client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Preflight(string origin, string method, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        return request;
    }

    private sealed class CorsFactory(params string[] origins) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly));
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                origins.Select((origin, index) => new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin))));
        }
    }
}
