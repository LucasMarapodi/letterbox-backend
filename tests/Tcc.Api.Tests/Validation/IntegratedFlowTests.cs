using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Feeds;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;
using Tcc.Api.Tests.Social;

namespace Tcc.Api.Tests.Validation;

public sealed class IntegratedFlowTests
{
    [Fact]
    public async Task Identity_opinions_social_dashboard_notifications_rankings_and_restart_share_consistent_state()
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        await using var host = new IdentityFactory(new MutableClock(now));
        await using var restarted = new IdentityFactory(new MutableClock(now));
        using var author = host.CreateClient();
        using var follower = host.CreateClient();
        using var fresh = restarted.CreateClient();
        var b = await SocialTest.Register(follower, "B");
        await ApiTest.SignIn(follower, "B@example.com", "p");
        var oldAuthorization = follower.DefaultRequestHeaders.Authorization;
        using var profile = await follower.PatchAsJsonAsync("/api/v1/users/me/profile", new ProfileUpdate("B edited"));
        profile.EnsureSuccessStatusCode();
        await SocialTest.Follow(follower, "user-1");
        await ApiTest.SignIn(author);
        using var rating = await author.PutAsJsonAsync("/api/v1/games/game-1/rating/me", new RatingInput(3.75));
        rating.EnsureSuccessStatusCode();
        var review = await ApiTest.Publish(author);
        using var gameFavorite = await follower.PutAsync("/api/v1/favorites/game/game-1", null);
        using var reviewFavorite = await follower.PutAsync($"/api/v1/favorites/review/{review.Id}", null);
        gameFavorite.EnsureSuccessStatusCode(); reviewFavorite.EnsureSuccessStatusCode();
        var dashboard = await follower.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard");
        Assert.NotNull(dashboard);
        Assert.Equal(review.Id, dashboard.Feed[0].Id);
        Assert.Equal(3.75, Assert.Single(dashboard.Favorites.Games).AverageRating);
        Assert.Equal(review.Id, Assert.Single(dashboard.Favorites.Reviews).Id);
        Assert.Equal("new-review", Assert.Single((await follower.GetFromJsonAsync<NotificationDto[]>("/api/v1/notifications")) ?? []).Type);
        using var posted = await follower.PostAsJsonAsync($"/api/v1/reviews/{review.Id}/comments", new TextInput("Comment"));
        posted.EnsureSuccessStatusCode();
        var eventItem = Assert.Single((await author.GetFromJsonAsync<NotificationDto[]>("/api/v1/notifications")) ?? []);
        Assert.Equal(b.Id, eventItem.ActorId);
        Assert.Equal("B edited", eventItem.Actor.DisplayName);
        Assert.Equal("review-comment", eventItem.Type);
        Assert.Equal(review.Id, Assert.Single((await author.GetFromJsonAsync<RankedReviewDto[]>("/api/v1/community/reviews/most-commented")) ?? []).Review.Id);
        Assert.Equal(1, Assert.Single((await author.GetFromJsonAsync<RankedGameDto[]>("/api/v1/community/games/in-discussion")) ?? []).CommentCount);
        using var logout = await follower.PostAsync("/api/v1/auth/logout", null);
        logout.EnsureSuccessStatusCode();
        using var blocked = await follower.PutAsync("/api/v1/favorites/game/game-2", null);
        Assert.Equal(401, (int)blocked.StatusCode);
        fresh.DefaultRequestHeaders.Authorization = oldAuthorization;
        using var oldToken = await fresh.GetAsync("/api/v1/auth/me");
        Assert.Equal(401, (int)oldToken.StatusCode);
        fresh.DefaultRequestHeaders.Authorization = null;
        using var missingAccount = await fresh.GetAsync($"/api/v1/users/{b.Id}");
        Assert.Equal(404, (int)missingAccount.StatusCode);
        Assert.Empty((await fresh.GetFromJsonAsync<ReviewDto[]>("/api/v1/games/game-1/reviews")) ?? []);
        Assert.Empty((await fresh.GetFromJsonAsync<RankedGameDto[]>("/api/v1/community/games/in-discussion")) ?? []);
        Assert.Null((await fresh.GetFromJsonAsync<GameDto>("/api/v1/games/game-1"))?.AverageRating);
    }

    [Fact]
    public async Task Concurrent_rating_replacement_keeps_one_id_and_one_local_vote()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var responses = await Task.WhenAll(client.PutAsJsonAsync("/api/v1/games/game-1/rating/me", new RatingInput(2)),
            client.PutAsJsonAsync("/api/v1/games/game-1/rating/me", new RatingInput(4)));
        try
        {
            Assert.All(responses, response => response.EnsureSuccessStatusCode());
            var first = await responses[0].Content.ReadFromJsonAsync<RatingDto>();
            var second = await responses[1].Content.ReadFromJsonAsync<RatingDto>();
            Assert.NotNull(first); Assert.NotNull(second);
            Assert.Equal(first.Id, second.Id);
            var mine = (await client.GetFromJsonAsync<MyRating>("/api/v1/games/game-1/rating/me"))?.Rating;
            Assert.NotNull(mine);
            Assert.Equal(mine.Value, (await client.GetFromJsonAsync<GameDto>("/api/v1/games/game-1"))?.AverageRating);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
    private sealed record ProfileUpdate(string DisplayName);
}
