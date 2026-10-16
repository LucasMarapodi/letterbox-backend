using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tcc.Application.Social;
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
    public async Task SQL_enforces_specific_FKs_pair_uniqueness_self_follow_and_preserves_creation_on_repetition()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var author = await Register(client, "constraints-author@example.com");
        var reader = await Register(client, "constraints-reader@example.com");
        using var scope = host.Services.CreateScope();
        var favorites = scope.ServiceProvider.GetRequiredService<IFavorites>();
        var follows = scope.ServiceProvider.GetRequiredService<IFollows>();
        var now = DateTimeOffset.UtcNow;
        var first = await favorites.SetAsync(reader.User, FavoriteKind.Game, game.Id, now);
        var saved = await db.GameFavorites.AsNoTracking().SingleAsync();
        var repeated = await favorites.SetAsync(reader.User, FavoriteKind.Game, game.Id, now.AddHours(1));
        Assert.Equal(first.Id, repeated.Id);
        Assert.Equal(saved.CreatedAt, (await db.GameFavorites.AsNoTracking().SingleAsync()).CreatedAt);
        db.Reviews.Add(Tcc.Domain.Opinions.Review.Create(0, author.User, game.Id, "Favorite review", now));
        await db.SaveChangesAsync();
        var review = await db.Reviews.AsNoTracking().SingleAsync();
        var firstReviewFavorite = await favorites.SetAsync(reader.User, FavoriteKind.Review, review.Id, now);
        var savedReviewFavorite = await db.ReviewFavorites.AsNoTracking().SingleAsync();
        var repeatedReviewFavorite = await favorites.SetAsync(reader.User, FavoriteKind.Review, review.Id, now.AddHours(1));
        Assert.Equal(firstReviewFavorite.Id, repeatedReviewFavorite.Id);
        Assert.Equal(savedReviewFavorite.CreatedAt, (await db.ReviewFavorites.AsNoTracking().SingleAsync()).CreatedAt);
        await follows.AddAsync(Follow.Create(reader.User, author.User, now));
        var original = await db.Follows.AsNoTracking().SingleAsync();
        await follows.AddAsync(Follow.Create(reader.User, author.User, now.AddHours(1)));
        Assert.Equal(original.CreatedAt, (await db.Follows.AsNoTracking().SingleAsync()).CreatedAt);
        var self = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO seguimentos_usuarios(seguidor_id,seguido_id,criado_em) VALUES({reader.User},{reader.User},{now})"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, self.SqlState);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO jogos_favoritos(usuario_id,jogo_id,criado_em) VALUES({reader.User},{game.Id},{now})"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        var absent = int.MaxValue;
        var invalidGame = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO jogos_favoritos(usuario_id,jogo_id,criado_em) VALUES({reader.User},{absent},{now})"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, invalidGame.SqlState);
        var invalidReview = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO reviews_favoritas(usuario_id,review_id,criado_em) VALUES({reader.User},{absent},{now})"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, invalidReview.SqlState);
        var invalidFollow = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO seguimentos_usuarios(seguidor_id,seguido_id,criado_em) VALUES({reader.User},{absent},{now})"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, invalidFollow.SqlState);
        var restricted = await Assert.ThrowsAsync<PostgresException>(() => db.Reviews.Where(item => item.Id == review.Id).ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, restricted.SqlState);
    }

    [PostgresFact]
    public async Task Persisted_followers_receive_new_review_notifications_with_generated_review_identity()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var author = await Register(client, "notify-author@example.com");
        var reader = await Register(client, "notify-reader@example.com");
        using var follow = await client.PutAsync($"/api/v1/users/{author.User}/follow", null);
        follow.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        using var published = await client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { text = "For SQL followers" });
        Assert.Equal(201, (int)published.StatusCode);
        using var review = JsonDocument.Parse(await published.Content.ReadAsStringAsync());
        var notification = Assert.Single(host.Services.GetRequiredService<INotifications>().ForUser(reader.User));
        Assert.Equal(review.RootElement.GetProperty("id").GetInt32(), notification.ReviewId);
        Assert.Equal(author.User, notification.ActorId);
        Assert.Empty(host.Services.GetRequiredService<INotifications>().ForUser(author.User));
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
        using var ownReview = await client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { text = "Own review must not enter followed feed" });
        ownReview.EnsureSuccessStatusCode();
        await Register(client, "social-outsider@example.com");
        using var unrelated = await client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { text = "Unfollowed review" });
        unrelated.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", reader.Token);
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
        foreach (var path in new[] { $"/api/v1/favorites/game/{game.Id}", $"/api/v1/favorites/review/{reviewId}" })
        {
            using var favorite = JsonDocument.Parse(await other.GetStringAsync(path));
            var restored = favorite.RootElement.GetProperty("favorite");
            Assert.True(restored.GetProperty("id").GetInt32() > 0);
            Assert.Equal(reader.User, restored.GetProperty("userId").GetInt32());
        }
        using var status = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/users/{author.User}/follow/me"));
        Assert.True(status.RootElement.GetProperty("following").GetBoolean());
        using var following = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/users/{reader.User}/following"));
        Assert.Equal(author.User, Assert.Single(following.RootElement.EnumerateArray()).GetProperty("id").GetInt32());
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
        using var reverseFeed = JsonDocument.Parse(await other.GetStringAsync("/api/v1/feed/following"));
        Assert.Equal(0, reverseFeed.RootElement.GetArrayLength());
        Assert.Equal(1, await db.Set<Follow>().CountAsync());
        Assert.Equal(1, await db.GameFavorites.CountAsync());
        Assert.Equal(1, await db.ReviewFavorites.CountAsync());
        other.DefaultRequestHeaders.Authorization = new("Bearer", reader.Token);
        foreach (var path in new[] { $"/api/v1/favorites/game/{game.Id}", $"/api/v1/favorites/review/{reviewId}", $"/api/v1/users/{author.User}/follow" })
        {
            for (var index = 0; index < 2; index++)
            {
                using var removed = await other.DeleteAsync(path);
                Assert.Equal(204, (int)removed.StatusCode);
            }
        }
        using var absentFavorite = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/favorites/review/{reviewId}"));
        Assert.Equal(JsonValueKind.Null, absentFavorite.RootElement.GetProperty("favorite").ValueKind);
        using var emptyFeed = JsonDocument.Parse(await other.GetStringAsync("/api/v1/feed/following"));
        Assert.Equal(0, emptyFeed.RootElement.GetArrayLength());
        Assert.Equal(0, await db.Follows.CountAsync());
        Assert.Equal(0, await db.GameFavorites.CountAsync());
        Assert.Equal(0, await db.ReviewFavorites.CountAsync());
    }
}
