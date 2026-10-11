using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Identity;

public sealed class SessionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Bearer fake")]
    [InlineData("Basic fake")]
    [InlineData("Bearer")]
    public async Task Missing_or_invalid_authorization_returns_standard_401(string? authorization)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        if (authorization is not null) client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        using var me = await client.GetAsync("/api/v1/auth/me");
        using var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, logout.StatusCode);
        Assert.Equal("unauthorized", (await me.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        Assert.Equal("Bearer", Assert.Single(me.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task Me_uses_token_identity_and_logout_revokes_only_that_session()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "p"));
        registration.EnsureSuccessStatusCode();
        var first = await LoginTests.Login(client);
        var sameAccount = await LoginTests.Login(client);
        var other = await LoginTests.Login(client, "b@example.com", "p");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.Token);
        using var me = await client.GetAsync($"/api/v1/auth/me?userId={other.User.Id}");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(first.User.Id, (await me.Content.ReadFromJsonAsync<PublicUser>())?.Id);
        using var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal("", await logout.Content.ReadAsStringAsync());
        using var revoked = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sameAccount.Token);
        using var sameMe = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, sameMe.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", other.Token);
        using var otherMe = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, otherMe.StatusCode);
        Assert.Equal(other.User.Id, (await otherMe.Content.ReadFromJsonAsync<PublicUser>())?.Id);
    }

    [Fact]
    public async Task Expiration_is_enforced_at_the_exact_boundary_and_after_it()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        var login = await LoginTests.Login(client);
        Assert.Equal(clock.GetUtcNow().AddMinutes(60), login.ExpiresAt);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        clock.UtcNow = login.ExpiresAt.AddTicks(-1);
        using var before = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        clock.UtcNow = login.ExpiresAt;
        using var boundary = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, boundary.StatusCode);
        clock.UtcNow = login.ExpiresAt.AddTicks(1);
        using var after = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task New_host_does_not_accept_previous_tokens()
    {
        await using var first = new WebApplicationFactory<Program>();
        await using var second = new WebApplicationFactory<Program>();
        using var a = first.CreateClient();
        using var b = second.CreateClient();
        var login = await LoginTests.Login(a);
        b.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        using var response = await b.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
