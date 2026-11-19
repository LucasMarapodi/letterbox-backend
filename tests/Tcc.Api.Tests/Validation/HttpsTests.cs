using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;

namespace Tcc.Api.Tests.Validation;

public sealed class HttpsTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/api/v1/auth/me")]
    public async Task Http_is_rejected_without_redirect_or_hsts(string path)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient(new() { BaseAddress = new("http://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync(path);
        Assert.Equal(400, (int)response.StatusCode);
        Assert.Equal("validation_failed", (await response.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Http_cannot_register_authenticate_or_preflight_even_with_forged_proxy_header()
    {
        await using var host = new TestApiFactory();
        using var insecure = host.CreateClient(new() { BaseAddress = new("http://localhost"), AllowAutoRedirect = false });
        insecure.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        var input = new RegisterInput("TLS test", "tls@example.com", "Abc1!x");
        using var registration = await insecure.PostAsJsonAsync("/api/v1/auth/register", input);
        Assert.Equal(400, (int)registration.StatusCode);
        using var login = await insecure.PostAsJsonAsync("/api/v1/auth/login", new LoginInput("ana@example.com", "teste123"));
        Assert.Equal(400, (int)login.StatusCode);
        using var options = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        options.Headers.Add("Origin", "https://localhost:5173");
        options.Headers.Add("Access-Control-Request-Method", "POST");
        using var preflight = await insecure.SendAsync(options);
        Assert.Equal(400, (int)preflight.StatusCode);
        Assert.Null(preflight.Headers.Location);
        using var secure = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        using var created = await secure.PostAsJsonAsync("/api/v1/auth/register", input);
        Assert.Equal(201, (int)created.StatusCode);
    }

    [Theory]
    [InlineData("https://localhost:5173", true)]
    [InlineData("http://localhost:5173", false)]
    [InlineData("https://untrusted.example", false)]
    public async Task Development_preflight_releases_only_explicit_secure_origin(string origin, bool allowed)
    {
        await using var host = new TestApiFactory().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        using var response = await client.SendAsync(request);
        Assert.Equal(204, (int)response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Https_scheme_keeps_documentation_and_health_accessible(string path)
    {
        // TestServer exercises scheme policy; a separate real listener must prove TLS/certificate trust.
        await using var host = new TestApiFactory();
        using var client = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync(path);
        Assert.Equal(200, (int)response.StatusCode);
    }
}
