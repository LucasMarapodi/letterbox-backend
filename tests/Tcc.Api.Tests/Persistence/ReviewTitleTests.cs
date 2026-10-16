using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tcc.Domain.Opinions;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class ReviewTitleTests
{
    [PostgresFact]
    public async Task Creation_matrix_normalizes_titles_enforces_limit_and_rejects_invalid_content_without_writes()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        await NotificationPersistenceTests.Register(client, "title-matrix@example.com");
        var games = await ProfileCompletionTests.Games();
        var valid = 0;
        foreach (var (title, text, code) in new (string?, string?, string?)[] {
            (null, null, null), ("  ", "  ", null), (" Title ", null, null), (" "+new string('t',150)+" ", " Body ", null),
            (null, "Body", "review_title_required"), ("  ", "Body", "review_title_required"), (new string('t',151), null, "review_title_too_long") })
        {
            using var response = await client.PostAsJsonAsync($"/api/v1/games/{games[Math.Min(valid,3)]}/reviews", new { rating = 0, title, text });
            Assert.Equal(code is null ? 201 : 400, (int)response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (code is not null) Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
            else
            {
                var expected = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
                Assert.Equal(expected, json.RootElement.GetProperty("title").GetString());
                Assert.Equal(text?.Trim() ?? "", json.RootElement.GetProperty("text").GetString());
                Assert.Equal(0, json.RootElement.GetProperty("rating").GetDouble());
                valid++;
            }
        }
        using (var missingRating = await client.PostAsJsonAsync($"/api/v1/games/{games[0]}/reviews", new { title = "Title", text = "Body" })) Assert.Equal(400, (int)missingRating.StatusCode);
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(4, await db.Reviews.CountAsync());
        Assert.Equal(4, await db.Ratings.CountAsync());
    }

    [PostgresFact]
    public async Task Partial_updates_keep_identity_links_title_and_rating_and_validate_the_final_state()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "title-edit@example.com");
        var game = (await ProfileCompletionTests.Games())[0];
        var id = await Publish(client, game, " Original ", " Body ");
        using (var comment = await client.PostAsJsonAsync($"/api/v1/reviews/{id}/comments", new { text = "Preserve" })) comment.EnsureSuccessStatusCode();
        using (var like = await client.PutAsync($"/api/v1/reviews/{id}/like", null)) like.EnsureSuccessStatusCode();
        using (var favorite = await client.PutAsync($"/api/v1/favorites/review/{id}", null)) favorite.EnsureSuccessStatusCode();
        await using var db = PostgresTestDatabase.Context();
        var before = await db.Reviews.AsNoTracking().SingleAsync();
        var ratings = await db.Ratings.AsNoTracking().SingleAsync();
        var events = await db.Notifications.AsNoTracking().ToArrayAsync();
        var likes = await db.ReviewLikes.AsNoTracking().ToArrayAsync();
        var comments = await db.Comments.AsNoTracking().ToArrayAsync();
        var favorites = await db.ReviewFavorites.AsNoTracking().ToArrayAsync();
        foreach (var body in new object[] { new { title = (string?)null }, new { title = " " }, new { title = new string('x',151) }, new { text = new string('x',10001) } })
        {
            using var invalid = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", body);
            Assert.Equal(400, (int)invalid.StatusCode);
        }
        Assert.Equal(before, await db.Reviews.AsNoTracking().SingleAsync());
        var changes = await Task.WhenAll(client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { title = " Renamed " }),
            client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { rating = 4.5 }));
        try { Assert.All(changes, response => Assert.Equal(200, (int)response.StatusCode)); }
        finally { foreach (var response in changes) response.Dispose(); }
        using (var legacyRating = await client.PutAsJsonAsync($"/api/v1/games/{game}/rating/me", new { value = 5 })) legacyRating.EnsureSuccessStatusCode();
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        using (var saved = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/reviews/{id}")))
        {
            Assert.Equal("Renamed", saved.RootElement.GetProperty("title").GetString());
            Assert.Equal("Body", saved.RootElement.GetProperty("text").GetString());
            Assert.Equal(5, saved.RootElement.GetProperty("rating").GetDouble());
            Assert.Equal(before.CreatedAt, saved.RootElement.GetProperty("createdAt").GetDateTimeOffset());
        }
        using (var opinions = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/games/{game}/opinions"))) Assert.Equal(5, opinions.RootElement.GetProperty("averageRating").GetDouble());
        using (var clear = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { title = (string?)null, text = (string?)null })) clear.EnsureSuccessStatusCode();
        using (var cleared = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{id}")))
        {
            Assert.Equal(JsonValueKind.Null, cleared.RootElement.GetProperty("title").ValueKind);
            Assert.Equal("", cleared.RootElement.GetProperty("text").GetString());
            Assert.Equal(id, cleared.RootElement.GetProperty("id").GetInt32());
        }
        Assert.Equal(ratings.Id, (await db.Ratings.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(events, await db.Notifications.AsNoTracking().ToArrayAsync());
        Assert.Equal(likes, await db.ReviewLikes.AsNoTracking().ToArrayAsync());
        Assert.Equal(comments, await db.Comments.AsNoTracking().ToArrayAsync());
        Assert.Equal(favorites, await db.ReviewFavorites.AsNoTracking().ToArrayAsync());
        await NotificationPersistenceTests.Register(client, "title-outsider@example.com");
        using var forbidden = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { title = "Forged" });
        Assert.Equal(403, (int)forbidden.StatusCode);
        Assert.Equal(owner.Id, (await db.Reviews.AsNoTracking().SingleAsync()).UserId);
    }

    [PostgresFact]
    public async Task All_common_projections_prioritize_titles_and_rankings_preserve_comment_count_first()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "title-order-owner@example.com");
        var games = await ProfileCompletionTests.Games();
        var plain = await Publish(client, games[0], null, null);
        var titled = await Publish(client, games[1], "First", "Body");
        var legacy = await Publish(client, games[2], null, null);
        var tied = await Publish(client, games[3], "Second", null);
        var now = DateTimeOffset.UtcNow;
        await using var db = PostgresTestDatabase.Context();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reviews SET criado_em={now.AddDays(-10)} WHERE id={titled} OR id={tied}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reviews SET texto={"Legacy"},titulo=NULL,criado_em={now.AddDays(-30)} WHERE id={legacy}");
        var reader = await NotificationPersistenceTests.Register(client, "title-order-reader@example.com");
        var other = await Publish(client, games[0], "Other", null);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reviews SET criado_em={now.AddDays(-3)} WHERE id={other}");
        using (var follow = await client.PutAsync($"/api/v1/users/{owner.Id}/follow", null)) follow.EnsureSuccessStatusCode();
        foreach (var (id,count) in new[] { (plain,3), (titled,2), (legacy,2), (tied,1) })
        {
            using (var favorite = await client.PutAsync($"/api/v1/favorites/review/{id}", null)) favorite.EnsureSuccessStatusCode();
            for (var index=0;index<count;index++)
            {
                using var comment = await client.PostAsJsonAsync($"/api/v1/reviews/{id}/comments", new { text = "Activity" });
                comment.EnsureSuccessStatusCode();
            }
        }
        foreach (var path in new[] { $"/api/v1/users/{owner.Id}/reviews", "/api/v1/feed/following" })
        {
            using var json = JsonDocument.Parse(await client.GetStringAsync(path));
            Assert.Equal(new[] { titled,tied,plain,legacy }, Ids(json.RootElement));
            Assert.All(json.RootElement.EnumerateArray(), item => Assert.True(item.TryGetProperty("title", out _)));
        }
        foreach (var path in new[] { $"/api/v1/games/{games[0]}/reviews", $"/api/v1/games/{games[0]}/opinions" })
        {
            using var json = JsonDocument.Parse(await client.GetStringAsync(path));
            var items = json.RootElement.ValueKind == JsonValueKind.Array ? json.RootElement : json.RootElement.GetProperty("reviews");
            Assert.Equal(new[] { other,plain }, Ids(items));
        }
        using (var recent = JsonDocument.Parse(await client.GetStringAsync("/api/v1/community/reviews/recent"))) Assert.Equal(new[] { other,titled,tied,plain,legacy }, Ids(recent.RootElement));
        using (var favorites = JsonDocument.Parse(await client.GetStringAsync("/api/v1/favorites"))) Assert.Equal(new[] { titled,tied,plain,legacy }, Ids(favorites.RootElement.GetProperty("reviews")));
        using (var dashboard = JsonDocument.Parse(await client.GetStringAsync("/api/v1/dashboard")))
        {
            Assert.Equal(new[] { titled,tied,plain,legacy }, Ids(dashboard.RootElement.GetProperty("feed")));
            Assert.Equal(new[] { titled,tied,plain,legacy }, Ids(dashboard.RootElement.GetProperty("favorites").GetProperty("reviews")));
        }
        using (var ranking = JsonDocument.Parse(await client.GetStringAsync("/api/v1/community/reviews/most-commented")))
            Assert.Equal(new[] { plain,titled,legacy,tied }, ranking.RootElement.EnumerateArray().Select(item => item.GetProperty("review").GetProperty("id").GetInt32()));
        client.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);
        using (var mine = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/games/{games[1]}/reviews/me"))) Assert.Equal("First", mine.RootElement.GetProperty("review").GetProperty("title").GetString());
        Assert.NotEqual(owner.Id, reader.Id);
    }

    [PostgresFact]
    public async Task Additive_migration_preserves_untitled_legacy_and_requires_title_only_when_writing()
    {
        await using var db = PostgresTestDatabase.Context();
        Assert.Contains(db.Database.GetMigrations(), name => name.EndsWith("ReviewTitle", StringComparison.Ordinal));
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "title-legacy@example.com");
        var game = (await ProfileCompletionTests.Games())[0];
        await PostgresTestDatabase.VerifyExclusiveAsync(db);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("ReviewLikes");
        int id;
        try
        {
            id = Assert.Single(await db.Database.SqlQuery<int>($"INSERT INTO reviews(usuario_id,jogo_id,texto,criado_em) VALUES({owner.Id},{game},{"Untitled legacy"},{DateTimeOffset.UtcNow}) RETURNING id AS \"Value\"").ToArrayAsync());
        }
        finally { await migrator.MigrateAsync(); }
        using (var read = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{id}")))
        {
            Assert.Equal("Untitled legacy", read.RootElement.GetProperty("text").GetString());
            Assert.Equal(JsonValueKind.Null, read.RootElement.GetProperty("title").ValueKind);
            Assert.Equal(JsonValueKind.Null, read.RootElement.GetProperty("rating").ValueKind);
        }
        using (var invalid = await client.PutAsJsonAsync($"/api/v1/games/{game}/rating/me", new { value = 3 })) Assert.Equal(400, (int)invalid.StatusCode);
        using (var missingRating = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { title = "Fixed" })) Assert.Equal(400, (int)missingRating.StatusCode);
        using (var fixedReview = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { title = "Fixed", rating = 0 })) fixedReview.EnsureSuccessStatusCode();
        using var saved = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{id}"));
        Assert.Equal(id, saved.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Fixed", saved.RootElement.GetProperty("title").GetString());
        Assert.Equal(0, saved.RootElement.GetProperty("rating").GetDouble());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static int[] Ids(JsonElement items) => items.EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray();
    private static async Task<int> Publish(HttpClient client, int game, string? title, string? text)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new { rating = 0, title, text });
        Assert.Equal(201, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }
}
