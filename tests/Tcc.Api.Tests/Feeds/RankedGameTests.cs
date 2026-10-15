using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;
using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;

namespace Tcc.Api.Tests.Feeds;

public sealed class RankedGameTests
{
    [Theory]
    [InlineData("24h", 24)]
    [InlineData("7d", 168)]
    [InlineData("30d", 720)]
    public async Task Game_ranking_sums_distinct_reviews_using_same_inclusive_window(string period, int hours)
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        await using var host = new IdentityFactory(new MutableClock(now));
        using var client = host.CreateClient();
        await RankingFixture.SeedReviews(host, client, now);
        RankingFixture.Comment(host, "a-lower", 101, now.AddHours(-hours));
        RankingFixture.Comment(host, "a-upper", 101, now);
        RankingFixture.Comment(host, "b", 102, now);
        RankingFixture.Comment(host, "c-lower", 103, now.AddHours(-hours));
        RankingFixture.Comment(host, "c-upper", 103, now);
        RankingFixture.Comment(host, "past", 101, now.AddHours(-hours).AddTicks(-1));
        RankingFixture.Comment(host, "future", 101, now.AddTicks(1));
        host.Services.GetRequiredService<IRatings>().Set(1, 3, RatingValue.Create(5));
        var results = await client.GetFromJsonAsync<RankedGameDto[]>($"/api/v1/community/games/in-discussion?period={period}");
        Assert.NotNull(results);
        Assert.Equal(new[] { 1, 2 }, results.Select(item => item.Game.Id));
        Assert.Equal(new[] { 3, 2 }, results.Select(item => item.CommentCount));
        Assert.All(results, item => Assert.Equal(now, item.LastCommentAt));
        if (period == "7d")
        {
            var defaultResults = await client.GetFromJsonAsync<RankedGameDto[]>("/api/v1/community/games/in-discussion");
            Assert.NotNull(defaultResults);
            Assert.Equal(results.Select(item => item.Game.Id), defaultResults.Select(item => item.Game.Id));
            Assert.Equal(results.Select(item => item.CommentCount), defaultResults.Select(item => item.CommentCount));
        }
    }

    [Fact]
    public async Task Games_use_last_eligible_comment_then_id_as_ties_and_match_review_windows()
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        await using var host = new IdentityFactory(new MutableClock(now));
        using var client = host.CreateClient();
        await RankingFixture.SeedReviews(host, client, now);
        RankingFixture.Comment(host, "a", 101, now.AddDays(-2));
        RankingFixture.Comment(host, "c", 103, now.AddDays(-1));
        var week = await client.GetFromJsonAsync<RankedGameDto[]>("/api/v1/community/games/in-discussion");
        Assert.Equal(new[] { 2, 1 }, (week ?? []).Select(item => item.Game.Id));
        var day = await client.GetFromJsonAsync<RankedGameDto[]>("/api/v1/community/games/in-discussion?period=24h");
        Assert.Equal(2, Assert.Single(day ?? []).Game.Id);
        RankingFixture.Comment(host, "b", 102, now.AddDays(-8));
        Assert.Equal(2, (await client.GetFromJsonAsync<RankedGameDto[]>("/api/v1/community/games/in-discussion?period=30d"))?[0].CommentCount);
        RankingFixture.Comment(host, "new-a", 101, now);
        RankingFixture.Comment(host, "new-c1", 103, now);
        RankingFixture.Comment(host, "new-c2", 103, now);
        var tied = await client.GetFromJsonAsync<RankedGameDto[]>("/api/v1/community/games/in-discussion?period=30d");
        Assert.Equal(new[] { 1, 2 }, (tied ?? []).Select(item => item.Game.Id));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    public async Task Invalid_period_returns_400(string period)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var response = await client.GetAsync($"/api/v1/community/games/in-discussion?period={period}");
        Assert.Equal(400, (int)response.StatusCode);
    }

    [Fact]
    public async Task Real_comment_on_old_review_updates_both_rankings_and_does_not_leak_to_new_host()
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        await using var host = new IdentityFactory(new MutableClock(now));
        await using var other = new IdentityFactory(new MutableClock(now));
        using var client = host.CreateClient();
        using var otherClient = other.CreateClient();
        Assert.Empty((await client.GetFromJsonAsync<RankedGameDto[]>("/api/v1/community/games/in-discussion")) ?? []);
        await ApiTest.SignIn(client);
        using var published = await client.PostAsJsonAsync("/api/v1/reviews/1/comments", new TextInput("Comment now"));
        published.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;
        var game = Assert.Single((await client.GetFromJsonAsync<RankedGameDto[]>("/api/v1/community/games/in-discussion")) ?? []);
        Assert.Equal(3, game.Game.Id);
        Assert.Equal(1, game.CommentCount);
        var review = Assert.Single((await client.GetFromJsonAsync<RankedReviewDto[]>("/api/v1/community/reviews/most-commented")) ?? []);
        Assert.Equal(1, review.Review.Id);
        Assert.Equal(now, review.LastCommentAt);
        Assert.Empty((await otherClient.GetFromJsonAsync<RankedGameDto[]>("/api/v1/community/games/in-discussion")) ?? []);
    }
}
