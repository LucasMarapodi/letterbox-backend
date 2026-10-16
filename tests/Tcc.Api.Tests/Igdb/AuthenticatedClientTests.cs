using System.Net;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class AuthenticatedClientTests
{
    [Fact]
    public async Task Rejected_token_is_replaced_once_and_request_body_is_recreated_for_fixed_IGDB_endpoint()
    {
        var clock = new TestClock();
        var oauthCalls = 0;
        using var oauthHttp = new HttpClient(new TestHandler((_, _) => Task.FromResult(IgdbTest.TokenResponse($"token-{++oauthCalls}"))));
        var source = new TwitchOAuthClient(oauthHttp, IgdbTest.Options(), clock);
        var tokens = new TwitchTokenProvider(source.AcquireAsync, clock);
        var igdbCalls = 0;
        using var http = new HttpClient(new TestHandler(async (request, ct) =>
        {
            var attempt = ++igdbCalls;
            Assert.Equal("https://api.igdb.com/v4/games", request.RequestUri?.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("synthetic-id", Assert.Single(request.Headers.GetValues("Client-ID")));
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal($"token-{attempt}", request.Headers.Authorization?.Parameter);
            Assert.NotNull(request.Content);
            Assert.Equal("fields id; limit 1;", await request.Content.ReadAsStringAsync(ct));
            return IgdbTest.Json("[]", attempt == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
        }));
        using var response = await new IgdbAuthenticatedClient(http, IgdbTest.Options(), tokens).PostGamesAsync("fields id; limit 1;");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, oauthCalls);
        Assert.Equal(2, igdbCalls);
    }

    [Fact]
    public async Task Persistent_401_has_no_loop_and_rejected_replacement_is_not_reused()
    {
        var clock = new TestClock();
        var oauthCalls = 0;
        using var oauthHttp = new HttpClient(new TestHandler((_, _) => Task.FromResult(IgdbTest.TokenResponse($"token-{++oauthCalls}"))));
        var source = new TwitchOAuthClient(oauthHttp, IgdbTest.Options(), clock);
        var tokens = new TwitchTokenProvider(source.AcquireAsync, clock);
        var igdbCalls = 0;
        using var http = new HttpClient(new TestHandler((_, _) => { igdbCalls++; return Task.FromResult(IgdbTest.Json("synthetic-secret", HttpStatusCode.Unauthorized)); }));
        var failure = await Assert.ThrowsAsync<ExternalIntegrationUnavailableException>(() =>
            new IgdbAuthenticatedClient(http, IgdbTest.Options(), tokens).PostGamesAsync("fields id; limit 1;"));
        Assert.DoesNotContain("synthetic", failure.ToString());
        Assert.Equal(2, igdbCalls);
        Assert.Equal(2, oauthCalls);
        Assert.Equal("token-3", (await tokens.GetAsync()).Value);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Non_authentication_errors_do_not_trigger_token_refresh_or_automatic_retry(int status)
    {
        var clock = new TestClock();
        var calls = 0;
        using var oauthHttp = new HttpClient(new TestHandler((_, _) => { calls++; return Task.FromResult(IgdbTest.TokenResponse()); }));
        var source = new TwitchOAuthClient(oauthHttp, IgdbTest.Options(), clock);
        var tokens = new TwitchTokenProvider(source.AcquireAsync, clock);
        using var http = new HttpClient(new TestHandler((_, _) => Task.FromResult(IgdbTest.Json("[]", (HttpStatusCode)status))));
        using var response = await new IgdbAuthenticatedClient(http, IgdbTest.Options(), tokens).PostGamesAsync("fields id; limit 1;");
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(1, calls);
    }
}
