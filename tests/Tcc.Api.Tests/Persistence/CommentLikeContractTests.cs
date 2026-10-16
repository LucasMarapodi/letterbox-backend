using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class CommentLikeContractTests
{
    [Theory]
    [InlineData("PUT", "")]
    [InlineData("DELETE", "")]
    [InlineData("GET", "/me")]
    public async Task Visitor_is_rejected_without_accessing_persistence(string method, string suffix)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.SendAsync(new(new HttpMethod(method), "/api/v1/comments/1/like" + suffix));
        Assert.Equal(401, (int)response.StatusCode);
    }

    [PostgresFact]
    public async Task Root_and_reply_likes_are_private_idempotent_durable_and_do_not_publish_events()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var owner = host.CreateClient();
        var author = await NotificationPersistenceTests.Register(owner, "comment-contract-owner@example.com");
        var review = await Review(owner);
        var root = await Comment(owner, review);
        var reply = await Comment(owner, review, root);
        using var reader = host.CreateClient();
        await NotificationPersistenceTests.Register(reader, "comment-contract-reader@example.com");
        await using var db = PostgresTestDatabase.Context();
        var notificationsBefore = await db.Notifications.CountAsync();

        foreach (var id in new[] { root, reply })
        {
            await Status(owner, id, false);
            await Status(reader, id, false);
            await ConcurrentCommands(owner, id, HttpMethod.Put);
            var original = await db.CommentLikes.AsNoTracking().SingleAsync(item => item.CommentId == id);
            using (var repeat = await owner.PutAsync($"/api/v1/comments/{id}/like", null)) await NoContent(repeat);
            Assert.Equal(original, await db.CommentLikes.AsNoTracking().SingleAsync(item => item.CommentId == id));
            await Status(owner, id, true);
            await Status(reader, id, false);
            await ConcurrentCommands(reader, id, HttpMethod.Put);
            await Counts(reader, review, root, reply, id == root ? 2 : 0, id == reply ? 2 : 0);
            await ConcurrentCommands(reader, id, HttpMethod.Delete);
            await Status(reader, id, false);
            await Status(owner, id, true);
            Assert.Equal(original, await db.CommentLikes.AsNoTracking().SingleAsync(item => item.CommentId == id));

            await using var restarted = new PostgresApiFactory();
            using var restored = restarted.CreateClient();
            restored.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
            await Status(restored, id, true);
            await ConcurrentCommands(restored, id, HttpMethod.Delete);
            await Status(restored, id, false);
            await Counts(reader, review, root, reply, 0, 0);
        }
        Assert.Equal(0, await db.CommentLikes.CountAsync());
        Assert.Equal(notificationsBefore, await db.Notifications.CountAsync());
        Assert.Equal(0, await db.ReviewLikes.CountAsync());
        Assert.Equal(0, await db.ReviewFavorites.CountAsync());
        Assert.Equal(0, await db.GameFavorites.CountAsync());
    }

    [PostgresFact]
    public async Task Invalid_ids_missing_targets_and_write_bodies_are_rejected_without_modifying_likes()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        await NotificationPersistenceTests.Register(client, "comment-contract-validation@example.com");
        var review = await Review(client);
        var id = await Comment(client, review);
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete, HttpMethod.Get })
        {
            var suffix = method == HttpMethod.Get ? "/me" : "";
            foreach (var target in new[] { "0", "-1", "not-an-int", "2147483648", "2147483647" })
            {
                using var response = await client.SendAsync(new(method, $"/api/v1/comments/{target}/like{suffix}"));
                Assert.Equal(target == "2147483647" ? 404 : 400, (int)response.StatusCode);
            }
            if (method == HttpMethod.Get) continue;
            using (var forged = await client.SendAsync(new(method, $"/api/v1/comments/{id}/like") { Content = JsonContent.Create(new { userId = 2 }) }))
                Assert.Equal(400, (int)forged.StatusCode);
            using (var body = await client.SendAsync(new(method, $"/api/v1/comments/{id}/like") { Content = new StringContent(" ") }))
                Assert.Equal(400, (int)body.StatusCode);
        }
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(0, await db.CommentLikes.CountAsync());
        await Status(client, id, false);
    }

    [PostgresFact]
    public async Task Public_counts_include_roots_and_replies_in_one_query_without_personal_state()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        await NotificationPersistenceTests.Register(client, "comment-contract-counts@example.com");
        var review = await Review(client);
        var root = await Comment(client, review);
        var reply = await Comment(client, review, root);
        for (var index = 0; index < 10; index++) await Comment(client, review);
        foreach (var id in new[] { root, reply })
        {
            using var response = await client.PutAsync($"/api/v1/comments/{id}/like", null);
            await NoContent(response);
        }
        var capture = new LikeReadCapture();
        await using var captured = host.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<TccDbContext>(options => options.AddInterceptors(capture))));
        using var publicClient = captured.CreateClient();
        var path = $"/api/v1/reviews/{review}/comments";
        var anonymous = await publicClient.GetStringAsync(path);
        Assert.Single(capture.Commands);
        using var json = JsonDocument.Parse(anonymous);
        Assert.Equal(12, json.RootElement.GetArrayLength());
        Assert.All(json.RootElement.EnumerateArray(), item =>
        {
            Assert.Equal(item.GetProperty("id").GetInt32() == root || item.GetProperty("id").GetInt32() == reply ? 1 : 0,
                item.GetProperty("likeCount").GetInt32());
            Assert.False(item.TryGetProperty("likedByMe", out _));
            Assert.False(item.TryGetProperty("liked", out _));
        });
        Assert.Equal(anonymous, await client.GetStringAsync(path));
        await Status(client, root, true);
    }

    private static async Task<int> Review(HttpClient client)
    {
        var game = (await ProfileCompletionTests.Games())[0];
        using var response = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new { rating = 3, title = "Fixture review", text = "Discussion" });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<int> Comment(HttpClient client, int review, int? parent = null)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/reviews/{review}/comments", new { text = "Comment", parentCommentId = parent });
        Assert.Equal(201, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task Status(HttpClient client, int id, bool expected)
    {
        using var response = await client.GetAsync($"/api/v1/comments/{id}/like/me");
        Assert.Equal(200, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("likedByMe", Assert.Single(json.RootElement.EnumerateObject()).Name);
        Assert.Equal(expected, json.RootElement.GetProperty("likedByMe").GetBoolean());
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(response.Headers.CacheControl?.Public == true);
    }

    private static async Task Counts(HttpClient client, int review, int root, int reply, int rootCount, int replyCount)
    {
        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{review}/comments"));
        var items = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(rootCount, Assert.Single(items, item => item.GetProperty("id").GetInt32() == root).GetProperty("likeCount").GetInt32());
        Assert.Equal(replyCount, Assert.Single(items, item => item.GetProperty("id").GetInt32() == reply).GetProperty("likeCount").GetInt32());
    }

    private static async Task NoContent(HttpResponseMessage response)
    {
        Assert.Equal(204, (int)response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    private static async Task ConcurrentCommands(HttpClient client, int id, HttpMethod method)
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.SendAsync(new(method, $"/api/v1/comments/{id}/like"))));
        try { foreach (var response in responses) await NoContent(response); }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    private sealed class LikeReadCapture : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("curtidas_comentarios", StringComparison.OrdinalIgnoreCase)) Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
