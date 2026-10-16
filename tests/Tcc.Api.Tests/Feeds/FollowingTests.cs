using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;
using Tcc.Api.Tests.Social;

namespace Tcc.Api.Tests.Feeds;

public sealed class FollowingTests
{
    [Fact]
    public async Task Feed_uses_current_follows_publication_order_and_session_identity()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        var b = await SocialTest.Register(client, "B");
        await SocialTest.Register(client, "C");
        await ApiTest.SignIn(client);
        var ana = await ApiTest.Publish(client);
        await ApiTest.SignIn(client, "C@example.com", "p");
        var c = await ApiTest.Publish(client);
        await ApiTest.SignIn(client, "B@example.com", "p");
        var own = await ApiTest.Publish(client);
        Assert.Empty((await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/feed/following")) ?? []);
        await SocialTest.Follow(client, "user-1");
        var feed = await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/feed/following?userId=user-1");
        Assert.NotNull(feed);
        Assert.Equal(new[] { ana.Id, "review-fixture" }, feed.Select(review => review.Id));
        Assert.DoesNotContain(feed, review => review.Id == own.Id || review.Id == c.Id);
        await SocialTest.Follow(client, c.UserId);
        feed = await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/feed/following");
        Assert.NotNull(feed);
        Assert.Equal(new[] { ana.Id, c.Id }.Order(StringComparer.Ordinal).Append("review-fixture"), feed.Select(review => review.Id));
        await ApiTest.SignIn(client);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var newest = await ApiTest.Publish(client, "game-2");
        await ApiTest.SignIn(client, "B@example.com", "p");
        Assert.Equal(newest.Id, (await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/feed/following"))?[0].Id);
        using var deleted = await client.DeleteAsync("/api/v1/users/user-1/follow");
        deleted.EnsureSuccessStatusCode();
        Assert.Equal(c.Id, Assert.Single((await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/feed/following")) ?? []).Id);
        await ApiTest.SignIn(client, "C@example.com", "p");
        Assert.Empty((await client.GetFromJsonAsync<ReviewDto[]>($"/api/v1/feed/following?userId={b.Id}")) ?? []);
    }

    [Fact]
    public async Task Anonymous_cannot_read_personal_feed()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/feed/following");
        Assert.Equal(401, (int)response.StatusCode);
    }
}
