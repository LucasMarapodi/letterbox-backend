using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class UnifiedReviewTests
{
    [PostgresFact]
    public async Task Migration_preserves_all_three_legacy_pairs_and_their_relationships_without_notifications()
    {
        await PostgresCatalogTests.Reset();
        var games = await ProfileCompletionTests.Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "unified-upgrade@example.com");
        await using var db = PostgresTestDatabase.Context();
        await PostgresTestDatabase.VerifyExclusiveAsync(db);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(db.Database.GetMigrations().Single(name => name.EndsWith("ProfileCompletionLocation", StringComparison.Ordinal)));
        var now = DateTimeOffset.UtcNow.AddDays(-7);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO reviews(usuario_id,jogo_id,texto,criado_em) VALUES({owner.Id},{games[0]},{"Rated legacy"},{now}),({owner.Id},{games[2]},{"Unrated legacy"},{now})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO avaliacoes(usuario_id,jogo_id,nota,criado_em,atualizado_em) VALUES({owner.Id},{games[0]},3.25,{now},{now}),({owner.Id},{games[1]},0,{now},{now})");
        var reviewIds = await db.Reviews.AsNoTracking().OrderBy(item => item.GameId).Select(item => item.Id).ToArrayAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO reviews_favoritas(usuario_id,review_id,criado_em) VALUES({owner.Id},{reviewIds[0]},{now})");
        try
        {
            await db.Database.MigrateAsync();
            Assert.Equal(3, await db.Reviews.CountAsync());
            Assert.Equal(2, await db.Ratings.CountAsync());
            Assert.Equal(reviewIds[0], (await db.ReviewFavorites.SingleAsync()).ReviewId);
            Assert.Equal(0, await db.Notifications.CountAsync());
            using var rated = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{reviewIds[0]}"));
            Assert.Equal(3.25, rated.RootElement.GetProperty("rating").GetDouble());
            using var pending = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{reviewIds[1]}"));
            Assert.Equal(JsonValueKind.Null, pending.RootElement.GetProperty("rating").ValueKind);
            using (var invalid = await client.PatchAsJsonAsync($"/api/v1/reviews/{reviewIds[1]}", new { text = "Requires rating" })) Assert.Equal(400, (int)invalid.StatusCode);
            using (var resolve = await client.PatchAsJsonAsync($"/api/v1/reviews/{reviewIds[1]}", new { rating = 0 })) resolve.EnsureSuccessStatusCode();
            Assert.Equal(3, await db.Ratings.CountAsync());
        }
        finally { await db.Database.MigrateAsync(); }
    }

    [PostgresFact]
    public async Task Rating_is_required_zero_is_valid_and_legacy_rating_updates_the_same_review()
    {
        await PostgresCatalogTests.Reset();
        var game = (await ProfileCompletionTests.Games())[0];
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        await NotificationPersistenceTests.Register(client, "unified@example.com");
        using (var missing = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new { text = "Text only" })) Assert.Equal(400, (int)missing.StatusCode);
        using var created = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new { rating = 0 });
        Assert.Equal(201, (int)created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(0, json.RootElement.GetProperty("rating").GetDouble());
        Assert.Equal("", json.RootElement.GetProperty("text").GetString());
        using (var edit = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { text = " Text ", rating = 3.25 })) edit.EnsureSuccessStatusCode();
        using (var legacy = await client.PutAsJsonAsync($"/api/v1/games/{game}/rating/me", new { value = 5 })) legacy.EnsureSuccessStatusCode();
        using var restored = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{id}"));
        Assert.Equal("Text", restored.RootElement.GetProperty("text").GetString());
        Assert.Equal(5, restored.RootElement.GetProperty("rating").GetDouble());
        await using var db = PostgresTestDatabase.Context();
        Assert.Single(await db.Reviews.ToArrayAsync());
        Assert.Single(await db.Ratings.ToArrayAsync());
    }

    [PostgresFact]
    public async Task Legacy_rating_creation_is_a_real_review_and_concurrent_publication_is_atomic()
    {
        await PostgresCatalogTests.Reset();
        var games = await ProfileCompletionTests.Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        await NotificationPersistenceTests.Register(client, "unified-concurrent@example.com");
        using (var legacy = await client.PutAsJsonAsync($"/api/v1/games/{games[0]}/rating/me", new { value = 0 })) legacy.EnsureSuccessStatusCode();
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(1, await db.Reviews.CountAsync());
        var results = await Task.WhenAll(Enumerable.Range(0,8).Select(index => client.PostAsJsonAsync($"/api/v1/games/{games[1]}/reviews", new { rating = index % 6, text = " " })));
        try
        {
            Assert.Single(results, result => (int)result.StatusCode == 201);
            Assert.Equal(7, results.Count(result => (int)result.StatusCode == 409));
        }
        finally { foreach (var result in results) result.Dispose(); }
        Assert.Equal(2, await db.Reviews.CountAsync());
        Assert.Equal(2, await db.Ratings.CountAsync());
        using var recent = JsonDocument.Parse(await client.GetStringAsync("/api/v1/community/reviews/recent"));
        Assert.All(recent.RootElement.EnumerateArray(), item => Assert.Equal(JsonValueKind.Number, item.GetProperty("rating").ValueKind));
    }
}
