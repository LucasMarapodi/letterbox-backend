using System.Net;
using System.Text.Json;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class OAuthTests
{
    [Fact]
    public async Task Acquires_by_fixed_HTTPS_form_body_and_validates_internal_token_and_expiry()
    {
        var clock = new TestClock();
        using var http = new HttpClient(new TestHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://id.twitch.tv/oauth2/token", request.RequestUri?.AbsoluteUri);
            Assert.Equal("", request.RequestUri?.Query);
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("application/x-www-form-urlencoded", request.Content?.Headers.ContentType?.MediaType);
            Assert.NotNull(request.Content);
            var form = (await request.Content.ReadAsStringAsync(ct)).Split('&').Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair[1]));
            Assert.Equal(3, form.Count);
            Assert.Equal("synthetic-id", form["client_id"]);
            Assert.Equal("synthetic-secret&+=", form["client_secret"]);
            Assert.Equal("client_credentials", form["grant_type"]);
            return IgdbTest.TokenResponse();
        }));
        var client = new TwitchOAuthClient(http, IgdbTest.Options(), clock);
        var token = await client.AcquireAsync();
        Assert.Equal("synthetic-token", token.Value);
        Assert.Equal(clock.GetUtcNow().AddSeconds(120), token.ExpiresAt);
        Assert.Equal(clock.GetUtcNow().AddSeconds(90), token.RefreshAt);
        Assert.DoesNotContain("synthetic", token.ToString());
        Assert.DoesNotContain("synthetic", JsonSerializer.Serialize(token));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("not-json synthetic-secret")]
    [InlineData("{\"access_token\":\"\",\"expires_in\":120,\"token_type\":\"bearer\"}")]
    [InlineData("{\"access_token\":\"bad token\",\"expires_in\":120,\"token_type\":\"bearer\"}")]
    [InlineData("{\"access_token\":\"synthetic-token\",\"expires_in\":0,\"token_type\":\"bearer\"}")]
    [InlineData("{\"access_token\":\"synthetic-token\",\"expires_in\":-10,\"token_type\":\"bearer\"}")]
    [InlineData("{\"access_token\":\"synthetic-token\",\"expires_in\":\"120\",\"token_type\":\"bearer\"}")]
    [InlineData("{\"access_token\":\"synthetic-token\",\"expires_in\":120,\"token_type\":\"basic\"}")]
    public async Task Malformed_payloads_have_safe_failure_without_supplier_body_or_inner_exception(string json)
    {
        using var http = new HttpClient(new TestHandler((_, _) => Task.FromResult(IgdbTest.Json(json))));
        var failure = await Assert.ThrowsAsync<ExternalIntegrationUnavailableException>(() =>
            new TwitchOAuthClient(http, IgdbTest.Options(), new TestClock()).AcquireAsync());
        Assert.DoesNotContain("synthetic", failure.ToString());
        Assert.DoesNotContain(json, failure.Message);
        Assert.Null(failure.InnerException);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Redirect_or_HTTP_failure_is_not_success_or_a_platform_unauthorized_error(int status)
    {
        using var http = new HttpClient(new TestHandler((_, _) =>
        {
            var response = IgdbTest.Json("synthetic-supplier-error", (HttpStatusCode)status);
            response.Headers.Location = new Uri("https://untrusted.example");
            return Task.FromResult(response);
        }));
        var failure = await Assert.ThrowsAsync<ExternalIntegrationUnavailableException>(() =>
            new TwitchOAuthClient(http, IgdbTest.Options(), new TestClock()).AcquireAsync());
        Assert.DoesNotContain("synthetic", failure.ToString());
    }

    [Fact]
    public async Task Transport_failure_does_not_expose_exception_containing_credentials()
    {
        using var http = new HttpClient(new TestHandler((_, _) => throw new HttpRequestException("synthetic-secret synthetic-token")));
        var failure = await Assert.ThrowsAsync<ExternalIntegrationUnavailableException>(() =>
            new TwitchOAuthClient(http, IgdbTest.Options(), new TestClock()).AcquireAsync());
        Assert.DoesNotContain("synthetic", failure.ToString());
        Assert.Null(failure.InnerException);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_and_timeout_is_sanitized()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var http = new HttpClient(new TestHandler((_, _) => throw new OperationCanceledException("synthetic-secret")));
        var client = new TwitchOAuthClient(http, IgdbTest.Options(), new TestClock());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.AcquireAsync(cancelled.Token));
        var failure = await Assert.ThrowsAsync<ExternalIntegrationUnavailableException>(() => client.AcquireAsync());
        Assert.DoesNotContain("synthetic", failure.ToString());
    }
}

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class IgdbTest
{
    public static IgdbCredentialOptions Options(string? margin = null) => IgdbCredentialOptions.Read(name => name switch
    {
        "IGDB__ClientId" => "synthetic-id",
        "IGDB__ClientSecret" => "synthetic-secret&+=",
        _ => margin
    });
    public static HttpResponseMessage TokenResponse(string token = "synthetic-token", int lifetime = 120) =>
        Json(JsonSerializer.Serialize(new { access_token = token, expires_in = lifetime, token_type = "bearer" }));
    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };
}
