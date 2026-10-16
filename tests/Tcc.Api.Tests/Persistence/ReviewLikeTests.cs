using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Collections.Concurrent;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class ReviewLikeTests
{
    [PostgresFact]
    public async Task All_review_projections_use_grouped_counts_without_personal_state_or_per_item_queries()
    {
        await PostgresCatalogTests.Reset();
        var games = await ProfileCompletionTests.Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var author = await NotificationPersistenceTests.Register(client, "review-like-projections@example.com");
        var ids = new List<int>();
        foreach (var game in games.Take(3))
        {
            using var publish = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new { rating = 0 });
            publish.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await publish.Content.ReadAsStringAsync());
            ids.Add(json.RootElement.GetProperty("id").GetInt32());
        }
        using (var like = await client.PutAsync($"/api/v1/reviews/{ids[0]}/like", null)) Assert.Equal(204, (int)like.StatusCode);
        await NotificationPersistenceTests.Register(client, "review-like-projection-reader@example.com");
        using (var like = await client.PutAsync($"/api/v1/reviews/{ids[0]}/like", null)) Assert.Equal(204, (int)like.StatusCode);
        using (var like = await client.PutAsync($"/api/v1/reviews/{ids[1]}/like", null)) Assert.Equal(204, (int)like.StatusCode);
        using (var follow = await client.PutAsync($"/api/v1/users/{author.Id}/follow", null)) follow.EnsureSuccessStatusCode();
        using (var favorite = await client.PutAsync($"/api/v1/favorites/review/{ids[0]}", null)) favorite.EnsureSuccessStatusCode();
        using (var comment = await client.PostAsJsonAsync($"/api/v1/reviews/{ids[0]}/comments", new { text = "Ranking" })) comment.EnsureSuccessStatusCode();
        var counts = new Dictionary<int,int> { [ids[0]]=2, [ids[1]]=1, [ids[2]]=0 };
        foreach (var path in new[] { $"/api/v1/reviews/{ids[0]}", $"/api/v1/games/{games[0]}/reviews", $"/api/v1/games/{games[0]}/opinions", $"/api/v1/users/{author.Id}/reviews", "/api/v1/community/reviews/recent", "/api/v1/community/reviews/most-commented", "/api/v1/feed/following", "/api/v1/favorites", "/api/v1/dashboard" })
        {
            using var json = JsonDocument.Parse(await client.GetStringAsync(path));
            var reviews = Reviews(json.RootElement).ToArray();
            Assert.NotEmpty(reviews);
            foreach (var item in reviews)
            {
                Assert.Equal(counts[item.GetProperty("id").GetInt32()], item.GetProperty("likeCount").GetInt32());
                Assert.False(item.TryGetProperty("likedByMe", out _));
            }
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        using (var mine = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/games/{games[0]}/reviews/me"))) Assert.Equal(2, mine.RootElement.GetProperty("review").GetProperty("likeCount").GetInt32());
        var capture = new LikeReadCapture();
        await using var captured = host.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddDbContext<TccDbContext>(options => options.AddInterceptors(capture))));
        using var publicClient = captured.CreateClient();
        using var publicResponse = await publicClient.GetAsync("/api/v1/community/reviews/recent");
        publicResponse.EnsureSuccessStatusCode();
        Assert.Contains("GROUP BY", Assert.Single(capture.Commands), StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<JsonElement> Reviews(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("author", out _) && value.TryGetProperty("gameId", out _)) yield return value;
            foreach (var property in value.EnumerateObject()) foreach (var review in Reviews(property.Value)) yield return review;
        }
        if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) foreach (var review in Reviews(item)) yield return review;
    }
    private sealed class LikeReadCapture : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("curtidas_reviews", StringComparison.OrdinalIgnoreCase)) Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    [Theory]
    [InlineData("PUT", "")]
    [InlineData("DELETE", "")]
    [InlineData("GET", "/me")]
    public async Task Anonymous_user_is_rejected_before_reading_SQL(string method, string suffix)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.SendAsync(new(new HttpMethod(method), "/api/v1/reviews/1/like" + suffix));
        Assert.Equal(401, (int)response.StatusCode);
    }

    [Fact]
    public void SQL_mapping_has_composite_integer_key_review_index_and_restrict_relationships()
    {
        using var db = new TccDbContext(new DbContextOptionsBuilder<TccDbContext>().UseNpgsql(BootstrapTests.SyntheticConnection).Options);
        var model = db.Model.FindEntityType("Tcc.Domain.Opinions.ReviewLike");
        Assert.NotNull(model);
        Assert.Equal("curtidas_reviews", model.GetTableName());
        Assert.Equal(new[] { "UserId", "ReviewId" }, model.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.All(model.FindPrimaryKey()!.Properties, property => Assert.Equal(typeof(int), property.ClrType));
        Assert.All(model.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
        Assert.Equal(2, model.GetForeignKeys().Count());
        Assert.Contains(model.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual(new[] { "ReviewId" }));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Concurrent_likes_are_idempotent_durable_private_and_independent_of_favorites_and_events()
    {
        await PostgresCatalogTests.Reset();
        var game = (await ProfileCompletionTests.Games())[0];
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "review-like-owner@example.com");
        using var published = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new { rating = 0 });
        published.EnsureSuccessStatusCode();
        using var review = JsonDocument.Parse(await published.Content.ReadAsStringAsync());
        var id = review.RootElement.GetProperty("id").GetInt32();
        var path = $"/api/v1/reviews/{id}/like";
        var results = await Task.WhenAll(Enumerable.Range(0,12).Select(_ => client.PutAsync(path, null)));
        try { Assert.All(results, result => Assert.Equal(204, (int)result.StatusCode)); }
        finally { foreach (var result in results) result.Dispose(); }
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        other.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);
        using (var status = await other.GetAsync(path + "/me"))
        {
            status.EnsureSuccessStatusCode();
            Assert.True(status.Headers.CacheControl?.NoStore);
            using var json = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("likedByMe").GetBoolean());
        }
        await AssertCount(other, id, 1);
        await NotificationPersistenceTests.Register(other, "review-like-second@example.com");
        using (var remove = await other.DeleteAsync(path)) Assert.Equal(204, (int)remove.StatusCode);
        await AssertCount(other, id, 1);
        using (var add = await other.PutAsync(path, null)) Assert.Equal(204, (int)add.StatusCode);
        await AssertCount(other, id, 2);
        for (var repeat=0; repeat<2; repeat++)
        {
            using var remove = await other.DeleteAsync(path);
            Assert.Equal(204, (int)remove.StatusCode);
        }
        using (var status = JsonDocument.Parse(await other.GetStringAsync(path + "/me"))) Assert.False(status.RootElement.GetProperty("likedByMe").GetBoolean());
        await AssertCount(other, id, 1);
        using (var remove = await client.DeleteAsync(path)) Assert.Equal(204, (int)remove.StatusCode);
        await AssertCount(other, id, 0);
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(0, await db.ReviewFavorites.CountAsync());
        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [PostgresFact]
    public async Task Invalid_ids_missing_targets_and_bodies_do_not_create_likes()
    {
        await PostgresCatalogTests.Reset();
        var game = (await ProfileCompletionTests.Games())[0];
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        await NotificationPersistenceTests.Register(client, "review-like-validation@example.com");
        using var publish = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new { rating = 3 });
        publish.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await publish.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetInt32();
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete, HttpMethod.Get })
        {
            var suffix = method == HttpMethod.Get ? "/me" : "";
            foreach (var target in new[] { "0", "-1", "not-an-int", "2147483648", "2147483647" })
            {
                using var result = await client.SendAsync(new(method, $"/api/v1/reviews/{target}/like{suffix}"));
                Assert.Equal(target == "2147483647" ? 404 : 400, (int)result.StatusCode);
            }
            if (method == HttpMethod.Get) continue;
            using var forged = await client.SendAsync(new(method, $"/api/v1/reviews/{id}/like") { Content = JsonContent.Create(new { userId = 2 }) });
            Assert.Equal(400, (int)forged.StatusCode);
        }
        await AssertCount(client, id, 0);
    }

    internal static async Task AssertCount(HttpClient client, int reviewId, int expected)
    {
        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{reviewId}"));
        Assert.Equal(expected, json.RootElement.GetProperty("likeCount").GetInt32());
        Assert.False(json.RootElement.TryGetProperty("likedByMe", out _));
    }
}
