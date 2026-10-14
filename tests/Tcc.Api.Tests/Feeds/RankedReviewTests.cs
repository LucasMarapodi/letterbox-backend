using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;

namespace Tcc.Api.Tests.Feeds;

public sealed record RankedReviewDto(ReviewDto Review, int CommentCount, DateTimeOffset LastCommentAt);
public sealed record RankedGameDto(GameDto Game, int CommentCount, DateTimeOffset LastCommentAt);

public sealed class RankedReviewTests
{
    [Theory]
    [InlineData("24h", 24)]
    [InlineData("7d", 168)]
    [InlineData("30d", 720)]
    public async Task Review_ranking_counts_both_boundaries_excludes_past_and_future_and_uses_deterministic_ties(string period, int hours)
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        await using var host = new IdentityFactory(new MutableClock(now));
        using var client = host.CreateClient();
        await RankingFixture.SeedReviews(host, client, now);
        RankingFixture.Comment(host, "lower", "rank-a", now.AddHours(-hours));
        RankingFixture.Comment(host, "upper", "rank-a", now);
        RankingFixture.Comment(host, "before", "rank-a", now.AddHours(-hours).AddTicks(-1));
        RankingFixture.Comment(host, "future", "rank-a", now.AddTicks(1));
        RankingFixture.Comment(host, "b", "rank-b", now);
        RankingFixture.Comment(host, "c", "rank-c", now);
        var results = await client.GetFromJsonAsync<RankedReviewDto[]>($"/api/v1/community/reviews/most-commented?period={period}");
        Assert.NotNull(results);
        Assert.Equal(new[] { "rank-a", "rank-b", "rank-c" }, results.Select(item => item.Review.Id));
        Assert.Equal(new[] { 2, 1, 1 }, results.Select(item => item.CommentCount));
        Assert.All(results, item => Assert.Equal(now, item.LastCommentAt));
        Assert.True(results[0].Review.CreatedAt < now.AddDays(-30));
        if (period == "7d") Assert.Equal(results, await client.GetFromJsonAsync<RankedReviewDto[]>("/api/v1/community/reviews/most-commented"));
    }

    [Fact]
    public async Task Windows_are_moving_hours_and_last_eligible_comment_breaks_count_ties()
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        await using var host = new IdentityFactory(new MutableClock(now));
        using var client = host.CreateClient();
        await RankingFixture.SeedReviews(host, client, now);
        RankingFixture.Comment(host, "a", "rank-a", now.AddDays(-8));
        RankingFixture.Comment(host, "b", "rank-b", now.AddDays(-2));
        RankingFixture.Comment(host, "c", "rank-c", now.AddDays(-1));
        var day = await client.GetFromJsonAsync<RankedReviewDto[]>("/api/v1/community/reviews/most-commented?period=24h");
        Assert.Equal("rank-c", Assert.Single(day ?? []).Review.Id);
        var week = await client.GetFromJsonAsync<RankedReviewDto[]>("/api/v1/community/reviews/most-commented");
        Assert.Equal(new[] { "rank-c", "rank-b" }, (week ?? []).Select(item => item.Review.Id));
        var month = await client.GetFromJsonAsync<RankedReviewDto[]>("/api/v1/community/reviews/most-commented?period=30d");
        Assert.Equal(new[] { "rank-c", "rank-b", "rank-a" }, (month ?? []).Select(item => item.Review.Id));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("24H")]
    [InlineData("")]
    public async Task Unknown_or_empty_period_returns_400(string period)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var response = await client.GetAsync($"/api/v1/community/reviews/most-commented?period={period}");
        Assert.Equal(400, (int)response.StatusCode);
    }

    [Fact]
    public async Task No_recent_comments_returns_empty_list()
    {
        await using var host = new IdentityFactory(new MutableClock(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero)));
        using var client = host.CreateClient();
        var result = await client.GetFromJsonAsync<RankedReviewDto[]>("/api/v1/community/reviews/most-commented");
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
