using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tcc.Api.DependencyInjection;
using Tcc.Api.Tests.Identity;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class MostLikedCommentTests
{
    private const string Path = "/api/v1/community/comments/most-liked";
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("period=")]
    [InlineData("period=24H")]
    [InlineData("period=1d")]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=1.5")]
    [InlineData("page=2147483648")]
    [InlineData("page=2147483647&pageSize=50")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=15")]
    [InlineData("pageSize=100")]
    [InlineData("pageSize=abc")]
    public async Task Invalid_query_returns_400_without_accessing_SQL(string query)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.GetAsync(Path + "?" + query);
        Assert.Equal(400, (int)response.StatusCode);
        Assert.Equal("validation_failed", (await response.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
    }

    [PostgresFact]
    public async Task Inclusive_UTC_like_windows_include_old_comments_and_replies_and_default_to_seven_days()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = Host(new MutableClock(Now));
        using var client = host.CreateClient();
        var seed = await Seed(client);
        await using var db = PostgresTestDatabase.Context();
        var dates = new[] { Now.AddDays(-30).AddTicks(-10), Now.AddDays(-30), Now.AddDays(-7).AddTicks(-10),
            Now.AddDays(-7), Now.AddDays(-1).AddTicks(-10), Now.AddDays(-1), Now, Now.AddTicks(10) };
        var comments = new List<Comment>();
        foreach (var date in dates)
        {
            var comment = await AddComment(db, seed.Owner, seed.Review, comments.Count == 6 ? comments[0].Id : null);
            comments.Add(comment);
            db.CommentLikes.Add(new(seed.Owner, comment.Id, date));
        }
        await AddComment(db, seed.Owner, seed.Review);
        db.CommentLikes.Add(new(seed.Reader, comments[6].Id, Now.AddDays(-40)));
        await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization = null;
        foreach (var (period, days) in new[] { ("24h", 1), ("7d", 7), ("30d", 30) })
        {
            using var result = await Read(client, "?period=" + period);
            var items = result.RootElement.GetProperty("items").EnumerateArray().ToArray();
            var expected = comments.Where((_, index) => dates[index] >= Now.AddDays(-days) && dates[index] <= Now).Select(item => item.Id).Order();
            Assert.Equal(expected, items.Select(item => item.GetProperty("commentId").GetInt32()).Order());
            Assert.All(items, item =>
            {
                Assert.Equal(1, item.GetProperty("periodLikeCount").GetInt32());
                Assert.Equal(seed.Review, item.GetProperty("reviewId").GetInt32());
                Assert.Equal(seed.Game, item.GetProperty("gameId").GetInt32());
                Assert.Equal(Now.AddYears(-2), item.GetProperty("createdAt").GetDateTimeOffset());
                Assert.Equal(new[] { "author", "commentId", "createdAt", "gameId", "lastLikeAt", "likeCount", "parentCommentId", "periodLikeCount", "reviewId", "text" },
                    item.EnumerateObject().Select(property => property.Name).Order());
                var author = item.GetProperty("author");
                Assert.Equal(new[] { "avatarUrl", "displayName", "id" }, author.EnumerateObject().Select(property => property.Name).Order());
                Assert.Equal(seed.Owner, author.GetProperty("id").GetInt32());
            });
            var reply = Assert.Single(items, item => item.GetProperty("commentId").GetInt32() == comments[6].Id);
            Assert.Equal(comments[0].Id, reply.GetProperty("parentCommentId").GetInt32());
            Assert.Equal(2, reply.GetProperty("likeCount").GetInt32());
            Assert.Equal(Now, reply.GetProperty("lastLikeAt").GetDateTimeOffset());
        }
        Assert.Equal(await client.GetStringAsync(Path + "?period=7d"), await client.GetStringAsync(Path));
    }

    [PostgresFact]
    public async Task PostgreSQL_orders_ties_and_pages_all_three_sizes_with_one_query_per_page()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = Host(new MutableClock(Now));
        using var client = host.CreateClient();
        var seed = await Seed(client);
        await using var db = PostgresTestDatabase.Context();
        var comments = new List<Comment>();
        for (var index = 0; index < 53; index++)
        {
            var comment = await AddComment(db, seed.Owner, seed.Review);
            comments.Add(comment);
            db.CommentLikes.Add(new(seed.Owner, comment.Id, index == 2 ? Now.AddHours(-3) : Now.AddHours(-1)));
        }
        foreach (var index in new[] { 0, 1 }) db.CommentLikes.Add(new(seed.Reader, comments[index].Id, Now.AddMinutes(-10)));
        db.CommentLikes.AddRange(new CommentLike(seed.Reader, comments[2].Id, Now.AddHours(-3)), new(seed.Third, comments[2].Id, Now.AddHours(-3)));
        await db.SaveChangesAsync();
        var capture = new Capture();
        await using var captured = host.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<TccDbContext>(options => options.AddInterceptors(capture))));
        using var anonymous = captured.CreateClient();
        var expected = new[] { comments[2].Id, comments[0].Id, comments[1].Id }.Concat(comments.Skip(3).Select(item => item.Id)).ToArray();
        foreach (var size in new[] { 10, 20, 50 })
        {
            var found = new List<int>();
            for (var page = 1; page <= (53 + size - 1) / size + 1; page++)
            {
                capture.Commands.Clear();
                using var result = await Read(anonymous, $"?page={page}&pageSize={size}");
                var envelope = result.RootElement;
                Assert.Equal(new[] { "hasNextPage", "items", "page", "pageSize" }, envelope.EnumerateObject().Select(property => property.Name).Order());
                Assert.Equal(page, envelope.GetProperty("page").GetInt32());
                Assert.Equal(size, envelope.GetProperty("pageSize").GetInt32());
                Assert.Equal(page * size < 53, envelope.GetProperty("hasNextPage").GetBoolean());
                var ids = envelope.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("commentId").GetInt32()).ToArray();
                Assert.Equal(expected.Skip((page - 1) * size).Take(size), ids);
                found.AddRange(ids);
                var sql = Assert.Single(capture.Commands);
                foreach (var fragment in new[] { "GROUP BY", "ORDER BY", "LIMIT" }) Assert.Contains(fragment, sql, StringComparison.OrdinalIgnoreCase);
                if (page > 1) Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
            }
            Assert.Equal(expected, found);
        }
    }

    [PostgresFact]
    public async Task Unlike_revalidates_immediately_and_relike_gets_a_new_date_without_refreshing_duplicate_puts()
    {
        await PostgresApiFactory.ResetAsync();
        var clock = new MutableClock(Now);
        await using var host = Host(clock);
        using var client = host.CreateClient();
        var seed = await Seed(client);
        await using var db = PostgresTestDatabase.Context();
        var comment = await AddComment(db, seed.Owner, seed.Review);
        db.CommentLikes.Add(new(seed.Owner, comment.Id, Now.AddDays(-8)));
        await db.SaveChangesAsync();
        var like = $"/api/v1/comments/{comment.Id}/like";
        using (var before = await Read(client)) Assert.Empty(before.RootElement.GetProperty("items").EnumerateArray());
        using (var repeat = await client.PutAsync(like, null)) Assert.Equal(204, (int)repeat.StatusCode);
        using (var stillOld = await Read(client)) Assert.Empty(stillOld.RootElement.GetProperty("items").EnumerateArray());
        using (var remove = await client.DeleteAsync(like)) Assert.Equal(204, (int)remove.StatusCode);
        using (var add = await client.PutAsync(like, null)) Assert.Equal(204, (int)add.StatusCode);
        using (var ranked = await Read(client)) Assert.Equal(Now, Assert.Single(ranked.RootElement.GetProperty("items").EnumerateArray()).GetProperty("lastLikeAt").GetDateTimeOffset());
        using (var remove = await client.DeleteAsync(like)) Assert.Equal(204, (int)remove.StatusCode);
        using (var empty = await Read(client)) Assert.Empty(empty.RootElement.GetProperty("items").EnumerateArray());
        clock.UtcNow = Now.AddMinutes(1);
        using (var add = await client.PutAsync(like, null)) Assert.Equal(204, (int)add.StatusCode);
        await using var restarted = Host(clock);
        using var other = restarted.CreateClient();
        using var restored = await Read(other);
        var item = Assert.Single(restored.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(1, item.GetProperty("likeCount").GetInt32());
        Assert.Equal(1, item.GetProperty("periodLikeCount").GetInt32());
        Assert.Equal(clock.UtcNow, item.GetProperty("lastLikeAt").GetDateTimeOffset());
    }

    [PostgresFact]
    public async Task Empty_database_is_public_and_uses_default_pagination()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = Host(new MutableClock(Now));
        using var client = host.CreateClient();
        using var result = await Read(client);
        Assert.Equal(1, result.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, result.RootElement.GetProperty("pageSize").GetInt32());
        Assert.False(result.RootElement.GetProperty("hasNextPage").GetBoolean());
        Assert.Empty(result.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Unavailable_database_returns_safe_503_without_fallback()
    {
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services => services.AddPostgresPersistence(_ =>
                "Host=127.0.0.1;Port=1;Database=unavailable;Username=synthetic;Password=synthetic-ranking-secret;Timeout=1"));
        });
        using var client = host.CreateClient();
        using var response = await client.GetAsync(Path);
        Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal("storage_unavailable", (await response.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        Assert.DoesNotContain("synthetic-ranking-secret", await response.Content.ReadAsStringAsync());
    }

    private static WebApplicationFactory<Program> Host(MutableClock clock) => new PostgresApiFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));

    private static async Task<(int Owner, int Reader, int Third, int Review, int Game)> Seed(HttpClient client)
    {
        var owner = await NotificationPersistenceTests.Register(client, "rank-owner@example.com");
        var reader = await NotificationPersistenceTests.Register(client, "rank-reader@example.com");
        var third = await NotificationPersistenceTests.Register(client, "rank-third@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);
        var game = (await ProfileCompletionTests.Games())[0];
        using var response = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new { rating = 3, text = "Ranking review" });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (owner.Id, reader.Id, third.Id, json.RootElement.GetProperty("id").GetInt32(), game);
    }

    private static async Task<Comment> AddComment(TccDbContext db, int owner, int review, int? parent = null)
    {
        var comment = Comment.Create(0, owner, review, "Old public comment", Now.AddYears(-2), parent);
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        return comment;
    }

    private static async Task<JsonDocument> Read(HttpClient client, string query = "")
    {
        using var response = await client.GetAsync(Path + query);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private sealed class Capture : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
