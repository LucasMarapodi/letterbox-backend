using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Opinions;
using Tcc.Api.Tests.Social;

namespace Tcc.Api.Tests.Feeds;

public sealed record DashboardDto(FavoriteLists Favorites, ReviewDto[] Feed);

public sealed class DashboardTests
{
    [Fact]
    public async Task Dashboard_has_stable_empty_structure_and_requires_session()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var anonymous = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(401, (int)anonymous.StatusCode);
        await ApiTest.SignIn(client);
        var dashboard = await client.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard");
        Assert.NotNull(dashboard);
        Assert.Empty(dashboard.Feed);
        Assert.Empty(dashboard.Favorites.Games);
        Assert.Empty(dashboard.Favorites.Reviews);
    }

    [Fact]
    public async Task Dashboard_composes_existing_queries_and_reflects_changes_without_leaking_other_accounts()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        var b = await SocialTest.Register(client, "B");
        await ApiTest.SignIn(client, "B@example.com", "p");
        await SocialTest.Follow(client, "user-1");
        using var game = await client.PutAsync("/api/v1/favorites/game/game-1", null);
        using var favoriteReview = await client.PutAsync("/api/v1/favorites/review/review-fixture", null);
        game.EnsureSuccessStatusCode(); favoriteReview.EnsureSuccessStatusCode();
        await ApiTest.SignIn(client);
        var newest = await ApiTest.Publish(client);
        using var rating = await client.PutAsJsonAsync("/api/v1/games/game-1/rating/me", new RatingInput(0));
        rating.EnsureSuccessStatusCode();
        var own = await client.GetFromJsonAsync<DashboardDto>($"/api/v1/dashboard?userId={b.Id}");
        Assert.NotNull(own);
        Assert.Empty(own.Favorites.Games);
        Assert.Empty(own.Feed);
        await ApiTest.SignIn(client, "B@example.com", "p");
        var dashboard = await client.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard?userId=user-1");
        Assert.NotNull(dashboard);
        Assert.Equal(newest.Id, dashboard.Feed[0].Id);
        Assert.Equal(0d, Assert.Single(dashboard.Favorites.Games).AverageRating);
        Assert.Equal("review-fixture", Assert.Single(dashboard.Favorites.Reviews).Id);
        var feed = await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/feed/following");
        Assert.Equal(feed, dashboard.Feed);
        using var remove = await client.DeleteAsync("/api/v1/favorites/game/game-1");
        using var unfollow = await client.DeleteAsync("/api/v1/users/user-1/follow");
        remove.EnsureSuccessStatusCode(); unfollow.EnsureSuccessStatusCode();
        dashboard = await client.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard");
        Assert.NotNull(dashboard);
        Assert.Empty(dashboard.Favorites.Games);
        Assert.Empty(dashboard.Feed);
        Assert.Single(dashboard.Favorites.Reviews);
    }
}
