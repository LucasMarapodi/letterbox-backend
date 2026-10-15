using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcc.Api.Tests.Identity;
using Tcc.Domain.Social;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class SocialPersistenceTests
{
    [Fact]
    public void Favorites_have_specific_foreign_keys_and_follows_have_directional_composite_key()
    {
        using var db = new TccDbContext(new DbContextOptionsBuilder<TccDbContext>().UseNpgsql(BootstrapTests.SyntheticConnection).Options);
        foreach (var name in new[] { "GameFavorite", "ReviewFavorite", "Follow" })
        {
            var model = Assert.Single(db.Model.GetEntityTypes(), entity => entity.ClrType.Name == name);
            Assert.All(model.FindPrimaryKey()!.Properties, property => Assert.Equal(typeof(int), property.ClrType));
            Assert.NotNull(model.FindProperty("CreatedAt"));
            Assert.Equal(2, model.GetForeignKeys().Count());
            Assert.All(model.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
            if (name != "Follow") Assert.Contains(model.GetIndexes(), index => index.IsUnique && index.Properties.Count == 2);
        }
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static async Task<(int User, string Token)> Register(HttpClient client, string email)
    {
        using var result = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Social", email, "synthetic-password"));
        result.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, email, "synthetic-password");
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        return (login.User.Id, login.Token);
    }

    [PostgresFact]
    public async Task Concurrent_favorites_following_feed_and_dashboard_survive_restart()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var author = await Register(client, "social-author@example.com");
        using var published = await client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { text = "Persisted social review" });
        published.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await published.Content.ReadAsStringAsync());
        var reviewId = body.RootElement.GetProperty("id").GetInt32();
        var reader = await Register(client, "social-reader@example.com");
        foreach (var path in new[] { $"/api/v1/favorites/game/{game.Id}", $"/api/v1/favorites/review/{reviewId}" })
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PutAsync(path, null)));
            try
            {
                var ids = new List<int>();
                foreach (var result in results)
                {
                    Assert.Equal(200, (int)result.StatusCode);
                    using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
                    ids.Add(json.RootElement.GetProperty("id").GetInt32());
                    Assert.True(json.RootElement.GetProperty("targetId").GetInt32() > 0);
                }
                Assert.Single(ids.Distinct());
            }
            finally { foreach (var result in results) result.Dispose(); }
        }
        var follows = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PutAsync($"/api/v1/users/{author.User}/follow", null)));
        try { Assert.All(follows, result => Assert.Equal(200, (int)result.StatusCode)); }
        finally { foreach (var result in follows) result.Dispose(); }
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        other.DefaultRequestHeaders.Authorization = new("Bearer", reader.Token);
        using var dashboard = JsonDocument.Parse(await other.GetStringAsync("/api/v1/dashboard"));
        Assert.Equal(game.Id, Assert.Single(dashboard.RootElement.GetProperty("favorites").GetProperty("games").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal(reviewId, Assert.Single(dashboard.RootElement.GetProperty("favorites").GetProperty("reviews").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal(reviewId, Assert.Single(dashboard.RootElement.GetProperty("feed").EnumerateArray()).GetProperty("id").GetInt32());
        using var network = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/users/{author.User}/followers"));
        Assert.Equal(reader.User, Assert.Single(network.RootElement.EnumerateArray()).GetProperty("id").GetInt32());
        other.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        using var own = JsonDocument.Parse(await other.GetStringAsync("/api/v1/favorites"));
        Assert.Equal(0, own.RootElement.GetProperty("games").GetArrayLength());
        Assert.Equal(0, own.RootElement.GetProperty("reviews").GetArrayLength());
        Assert.Equal(1, await db.Set<Follow>().CountAsync());
    }
}
