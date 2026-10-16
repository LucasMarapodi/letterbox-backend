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
    public async Task Invalid_commands_do_not_write_and_concurrent_partial_edits_preserve_omitted_fields()
    {
        await PostgresCatalogTests.Reset();
        var game = (await ProfileCompletionTests.Games())[0];
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var author = await NotificationPersistenceTests.Register(client, "unified-validation@example.com");
        var route = $"/api/v1/games/{game}/reviews";
        foreach (var body in new object[] { new { rating = -1 }, new { rating = 5.01 }, new { rating = (double?)null }, new { rating = "3" }, new { title = "Fixture review", rating = 3, text = new string('x',10001) }, new { rating = 3, userId = author.Id } })
        {
            using var invalid = await client.PostAsJsonAsync(route, body);
            Assert.Equal(400, (int)invalid.StatusCode);
        }
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(0, await db.Ratings.CountAsync());
        Assert.Equal(0, await db.Reviews.CountAsync());
        using var created = await client.PostAsJsonAsync(route, new { title = "Fixture review", rating = 0, text = "Original" });
        created.EnsureSuccessStatusCode();
        using var first = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = first.RootElement.GetProperty("id").GetInt32();
        var ratingId = (await db.Ratings.AsNoTracking().SingleAsync()).Id;
        var edits = await Task.WhenAll(client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { rating = 3.25 }),
            client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { text = "Changed" }));
        try { Assert.All(edits, result => Assert.Equal(200, (int)result.StatusCode)); }
        finally { foreach (var response in edits) response.Dispose(); }
        using var result = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{id}"));
        Assert.Equal("Changed", result.RootElement.GetProperty("text").GetString());
        Assert.Equal(3.25, result.RootElement.GetProperty("rating").GetDouble());
        Assert.Equal(first.RootElement.GetProperty("createdAt").GetString(), result.RootElement.GetProperty("createdAt").GetString());
        Assert.Equal(ratingId, (await db.Ratings.AsNoTracking().SingleAsync()).Id);
        using (var clear = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { text = (string?)null })) clear.EnsureSuccessStatusCode();
        await NotificationPersistenceTests.Register(client, "unified-outsider@example.com");
        using var forbidden = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { rating = 5 });
        Assert.Equal(403, (int)forbidden.StatusCode);
        using var missing = await client.PostAsJsonAsync("/api/v1/games/2147483647/reviews", new { rating = 5 });
        Assert.Equal(404, (int)missing.StatusCode);
    }

    [PostgresFact]
    public async Task Rating_only_review_is_present_in_every_projection_and_keeps_comments_likes_favorites_and_events_on_edit()
    {
        await PostgresCatalogTests.Reset();
        var game = (await ProfileCompletionTests.Games())[0];
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var author = await NotificationPersistenceTests.Register(client, "unified-author@example.com");
        var reader = await NotificationPersistenceTests.Register(client, "unified-reader@example.com");
        using (var follow = await client.PutAsync($"/api/v1/users/{author.Id}/follow", null)) follow.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        using var created = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new { title = "Fixture review", rating = 0, text = " " });
        created.EnsureSuccessStatusCode();
        using var published = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = published.RootElement.GetProperty("id").GetInt32();
        client.DefaultRequestHeaders.Authorization = new("Bearer", reader.Token);
        using (var favorite = await client.PutAsync($"/api/v1/favorites/review/{id}", null)) favorite.EnsureSuccessStatusCode();
        using var comment = await client.PostAsJsonAsync($"/api/v1/reviews/{id}/comments", new { text = "Comment" });
        comment.EnsureSuccessStatusCode();
        using var commentJson = JsonDocument.Parse(await comment.Content.ReadAsStringAsync());
        using (var like = await client.PutAsync($"/api/v1/comments/{commentJson.RootElement.GetProperty("id").GetInt32()}/like", null)) like.EnsureSuccessStatusCode();
        var reads = new[] { $"/api/v1/reviews/{id}", $"/api/v1/users/{author.Id}/reviews", $"/api/v1/games/{game}/reviews", $"/api/v1/games/{game}/opinions", "/api/v1/community/reviews/recent", "/api/v1/community/reviews/most-commented", "/api/v1/feed/following", "/api/v1/favorites", "/api/v1/dashboard" };
        foreach (var path in reads)
        {
            using var json = JsonDocument.Parse(await client.GetStringAsync(path));
            var reviews = FindReviews(json.RootElement).ToArray();
            Assert.NotEmpty(reviews);
            Assert.All(reviews, review => { Assert.Equal(0, review.GetProperty("rating").GetDouble()); Assert.Equal("", review.GetProperty("text").GetString()); });
        }
        await using var db = PostgresTestDatabase.Context();
        var events = await db.Notifications.AsNoTracking().ToArrayAsync();
        var favoriteRows = await db.ReviewFavorites.AsNoTracking().ToArrayAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        using (var mine = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/games/{game}/reviews/me"))) Assert.Equal(0, mine.RootElement.GetProperty("review").GetProperty("rating").GetDouble());
        using (var update = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { rating = 4.5 })) update.EnsureSuccessStatusCode();
        Assert.Equal(events, await db.Notifications.AsNoTracking().ToArrayAsync());
        Assert.Equal(favoriteRows, await db.ReviewFavorites.AsNoTracking().ToArrayAsync());
        Assert.Equal(1, await db.Comments.CountAsync());
        Assert.Equal(1, await db.CommentLikes.CountAsync());
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        using var restored = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/reviews/{id}"));
        Assert.Equal(4.5, restored.RootElement.GetProperty("rating").GetDouble());
        using var opinions = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/games/{game}/opinions"));
        Assert.Equal(4.5, opinions.RootElement.GetProperty("averageRating").GetDouble());
    }

    private static IEnumerable<JsonElement> FindReviews(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("author", out _) && value.TryGetProperty("gameId", out _)) yield return value;
            foreach (var property in value.EnumerateObject()) foreach (var review in FindReviews(property.Value)) yield return review;
        }
        if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) foreach (var review in FindReviews(item)) yield return review;
    }

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
        // This historical schema predates Title; create the relationship without querying a current Review model.
        db.Comments.Add(Tcc.Domain.Opinions.Comment.Create(0, owner.Id, reviewIds[0], "Preserved legacy comment", now));
        await db.SaveChangesAsync();
        var commentId = (await db.Comments.AsNoTracking().SingleAsync()).Id;
        using (var like = await client.PutAsync($"/api/v1/comments/{commentId}/like", null)) like.EnsureSuccessStatusCode();
        var ratingsBefore = await db.Ratings.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var commentsBefore = await db.Comments.AsNoTracking().ToArrayAsync();
        var likesBefore = await db.CommentLikes.AsNoTracking().ToArrayAsync();
        var favoritesBefore = await db.ReviewFavorites.AsNoTracking().ToArrayAsync();
        try
        {
            await db.Database.MigrateAsync();
            Assert.Equal(3, await db.Reviews.CountAsync());
            Assert.Equal(2, await db.Ratings.CountAsync());
            Assert.Equal(ratingsBefore, await db.Ratings.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync());
            Assert.Equal(commentsBefore, await db.Comments.AsNoTracking().ToArrayAsync());
            Assert.Equal(likesBefore, await db.CommentLikes.AsNoTracking().ToArrayAsync());
            Assert.Equal(favoritesBefore, await db.ReviewFavorites.AsNoTracking().ToArrayAsync());
            var imported = await db.Reviews.AsNoTracking().SingleAsync(item => item.GameId == games[1]);
            Assert.Equal("", imported.Text);
            Assert.Equal(ratingsBefore[1].CreatedAt, imported.CreatedAt);
            Assert.Equal(ratingsBefore[1].UpdatedAt, imported.UpdatedAt);
            Assert.Equal(reviewIds[0], (await db.ReviewFavorites.SingleAsync()).ReviewId);
            Assert.Equal(0, await db.Notifications.CountAsync());
            using var rated = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{reviewIds[0]}"));
            Assert.Equal(3.25, rated.RootElement.GetProperty("rating").GetDouble());
            using var pending = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{reviewIds[1]}"));
            Assert.Equal(JsonValueKind.Null, pending.RootElement.GetProperty("rating").ValueKind);
            using (var invalid = await client.PatchAsJsonAsync($"/api/v1/reviews/{reviewIds[1]}", new { text = "Requires rating" })) Assert.Equal(400, (int)invalid.StatusCode);
            using (var resolve = await client.PatchAsJsonAsync($"/api/v1/reviews/{reviewIds[1]}", new { rating = 0, title = "Resolved legacy" })) resolve.EnsureSuccessStatusCode();
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
        using (var edit = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { title = "Fixture review", text = " Text ", rating = 3.25 })) edit.EnsureSuccessStatusCode();
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
        var results = await Task.WhenAll(Enumerable.Range(0,8).Select(index => client.PostAsJsonAsync($"/api/v1/games/{games[1]}/reviews", new { title = "Fixture review", rating = index % 6, text = " " })));
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
