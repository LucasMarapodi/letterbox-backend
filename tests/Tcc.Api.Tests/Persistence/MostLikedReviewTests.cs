using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Tests.Identity;
using Tcc.Domain.Identity;
using Tcc.Domain.Opinions;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class MostLikedReviewTests
{
    private const string Path = "/api/v1/community/reviews/most-liked";
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("period=")]
    [InlineData("period=24H")]
    [InlineData("page=0")]
    [InlineData("page=2147483648")]
    [InlineData("page=2147483647&pageSize=50")]
    [InlineData("pageSize=15")]
    public async Task Invalid_query_returns_400_without_SQL(string query)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var response = await host.CreateClient().GetAsync(Path + "?" + query);
        Assert.Equal(400, (int)response.StatusCode);
    }

    [PostgresFact]
    public async Task Public_review_likes_use_inclusive_windows_SQL_order_pagination_and_current_active_rows()
    {
        await PostgresApiFactory.ResetAsync();
        var game = (await ProfileCompletionTests.Games())[0];
        await using var db = PostgresTestDatabase.Context();
        var accounts = Enumerable.Range(0, 24).Select(i => new UserAccount(0, "", Email.Create($"review-rank-{i}@example.com"),
            "synthetic", UserProfile.Create($"Author {i}", ""), Now.AddYears(-2))).ToArray();
        db.Users.AddRange(accounts);
        await db.SaveChangesAsync();
        var reviews = accounts.Take(21).Select(account => Review.CreateRated(account.Id, game, "Old review", 0, Now.AddYears(-1), "Old title")).ToArray();
        db.Reviews.AddRange(reviews);
        await db.SaveChangesAsync();
        foreach (var review in reviews) db.ReviewLikes.Add(new(accounts[21].Id, review.Id, Now.AddDays(-7)));
        db.ReviewLikes.AddRange(new ReviewLike(accounts[22].Id, reviews[0].Id, Now),
            new(accounts[23].Id, reviews[0].Id, Now.AddDays(-40)),
            new(accounts[22].Id, reviews[1].Id, Now.AddDays(-1)),
            new(accounts[23].Id, reviews[1].Id, Now.AddTicks(10)));
        await db.SaveChangesAsync();
        await using var host = new PostgresApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new MutableClock(Now))));
        using var client = host.CreateClient();
        using var response = await client.GetAsync(Path);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = result.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(20, items.Length);
        Assert.True(result.RootElement.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(reviews[0].Id, items[0].GetProperty("review").GetProperty("id").GetInt32());
        Assert.Equal(2, items[0].GetProperty("periodLikeCount").GetInt32());
        Assert.Equal(3, items[0].GetProperty("review").GetProperty("likeCount").GetInt32());
        Assert.Equal("Old title", items[0].GetProperty("review").GetProperty("title").GetString());
        Assert.Equal(0, items[0].GetProperty("review").GetProperty("rating").GetDouble());
        Assert.Equal(game, items[0].GetProperty("review").GetProperty("game").GetProperty("id").GetInt32());
        Assert.True(items[0].GetProperty("review").GetProperty("game").TryGetProperty("coverUrl", out _));
        Assert.Equal("Author 0", items[0].GetProperty("review").GetProperty("author").GetProperty("displayName").GetString());
        Assert.DoesNotContain("likedByMe", result.RootElement.GetRawText());
        Assert.DoesNotContain("email", result.RootElement.GetRawText());
        Assert.Equal(reviews[1].Id, items[1].GetProperty("review").GetProperty("id").GetInt32());
        Assert.Equal(reviews[2].Id, items[2].GetProperty("review").GetProperty("id").GetInt32());
        using var next = await client.GetFromJsonAsync<JsonDocument>(Path + "?page=2");
        Assert.Single(next!.RootElement.GetProperty("items").EnumerateArray());
        Assert.False(next.RootElement.GetProperty("hasNextPage").GetBoolean());
        using var day = await client.GetFromJsonAsync<JsonDocument>(Path + "?period=24h&pageSize=10");
        Assert.Equal(2, day!.RootElement.GetProperty("items").GetArrayLength());
        db.ReviewLikes.RemoveRange(db.ReviewLikes.Where(like => like.ReviewId == reviews[0].Id));
        await db.SaveChangesAsync();
        using var refreshed = await client.GetFromJsonAsync<JsonDocument>(Path + "?period=30d&pageSize=50");
        Assert.Equal(reviews[1].Id, refreshed!.RootElement.GetProperty("items")[0].GetProperty("review").GetProperty("id").GetInt32());
        Assert.Equal(20, refreshed.RootElement.GetProperty("items").GetArrayLength());
    }
}
