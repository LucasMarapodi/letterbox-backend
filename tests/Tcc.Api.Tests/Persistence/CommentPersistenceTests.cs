using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tcc.Application.Social;
using Tcc.Api.Tests.Social;
using Tcc.Infrastructure.Social;
using Tcc.Api.Tests.Identity;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class CommentPersistenceTests
{
    private static async Task<(int User, int Review)> Seed(HttpClient client)
    {
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Writer", "discussion@example.com", "synthetic-password"));
        registered.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, "discussion@example.com", "synthetic-password");
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        using var response = await client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { title = "Fixture review", rating = 3, text = "Review" });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (login.User.Id, json.RootElement.GetProperty("id").GetInt32());
    }
    private static async Task<int> Publish(HttpClient client, int reviewId, int? parent = null)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/comments", new { text = "Comment", parentCommentId = parent });
        Assert.Equal(201, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    [PostgresFact]
    public async Task Migration_down_up_on_exclusive_database_preserves_existing_users_and_reviews()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var (_, reviewId) = await Seed(client);
        await using var db = PostgresTestDatabase.Context();
        await PostgresTestDatabase.VerifyExclusiveAsync(db);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("Opinions");
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.True(await db.Reviews.AnyAsync(review => review.Id == reviewId));
        await migrator.MigrateAsync();
        Assert.Equal(0, await db.Comments.CountAsync());
        Assert.Equal(0, await db.CommentLikes.CountAsync());
        await Publish(client, reviewId);
    }

    [PostgresFact]
    public async Task SQL_rankings_include_replies_and_exclude_past_and_future_comments()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var (userId, reviewId) = await Seed(client);
        var root = await Publish(client, reviewId);
        await Publish(client, reviewId, root);
        await using var db = PostgresTestDatabase.Context();
        var now = DateTimeOffset.UtcNow;
        db.Comments.AddRange(Comment.Create(0, userId, reviewId, "Past", now.AddDays(-8)),
            Comment.Create(0, userId, reviewId, "Future", now.AddDays(1)));
        await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization = null;
        foreach (var path in new[] { "/api/v1/community/reviews/most-commented", "/api/v1/community/games/in-discussion" })
        {
            using var response = JsonDocument.Parse(await client.GetStringAsync(path));
            var result = Assert.Single(response.RootElement.EnumerateArray());
            Assert.Equal(2, result.GetProperty("commentCount").GetInt32());
        }
    }

    [PostgresFact]
    public async Task SQL_rejects_cross_review_second_level_cycles_text_and_invalid_like_relationships()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var (userId, reviewId) = await Seed(client);
        var root = await Publish(client, reviewId);
        var reply = await Publish(client, reviewId, root);
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload(4_000_000_001)], default)).Game;
        db.Reviews.Add(Review.Create(0, userId, game.Id, "Other review", DateTimeOffset.UtcNow, "Fixture review"));
        await db.SaveChangesAsync();
        var otherReview = await db.Reviews.SingleAsync(item => item.GameId == game.Id && item.UserId == userId);
        using var cross = await client.PostAsJsonAsync($"/api/v1/reviews/{otherReview.Id}/comments", new { text = "Cross review", parentCommentId = root });
        Assert.Equal(400, (int)cross.StatusCode);
        var crossSql = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO comentarios(usuario_id,review_id,comentario_pai_id,texto,criado_em) VALUES({userId},{otherReview.Id},{root},{"Cross review"},now())"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, crossSql.SqlState);
        var nested = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO comentarios(usuario_id,review_id,comentario_pai_id,texto,criado_em) VALUES({userId},{reviewId},{reply},{"Nested"},now())"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, nested.SqlState);
        foreach (var parent in new[] { root, reply })
        {
            var cycle = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE comentarios SET comentario_pai_id={parent} WHERE id={root}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, cycle.SqlState);
        }
        var invalidText = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO comentarios(usuario_id,review_id,texto,criado_em) VALUES({userId},{reviewId},{"  "},now())"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidText.SqlState);
        var absent = int.MaxValue;
        var invalidLike = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO curtidas_comentarios(usuario_id,comentario_id,criado_em) VALUES({userId},{absent},now())"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, invalidLike.SqlState);
        using var liked = await client.PutAsync($"/api/v1/comments/{root}/like", null);
        Assert.Equal(204, (int)liked.StatusCode);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO curtidas_comentarios(usuario_id,comentario_id,criado_em) VALUES({userId},{root},now())"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        var restricted = await Assert.ThrowsAsync<PostgresException>(() => db.Comments.Where(item => item.Id == root).ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, restricted.SqlState);
        Assert.Equal(2, await db.Comments.CountAsync());
        Assert.Equal(1, await db.CommentLikes.CountAsync());
    }

    [PostgresFact]
    public async Task Likes_are_personal_counts_are_public_and_bodies_or_missing_targets_are_rejected()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var (_, reviewId) = await Seed(client);
        var root = await Publish(client, reviewId);
        using var invalidReview = await client.PostAsJsonAsync("/api/v1/reviews/0/comments", new { text = "Invalid review" });
        Assert.Equal(400, (int)invalidReview.StatusCode);
        using var own = await client.PutAsync($"/api/v1/comments/{root}/like", null);
        Assert.Equal(204, (int)own.StatusCode);
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Other", "other-like@example.com", "synthetic-password"));
        registered.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, "other-like@example.com", "synthetic-password");
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        using var status = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/comments/{root}/like/me"));
        Assert.False(status.RootElement.GetProperty("likedByMe").GetBoolean());
        using var forged = await client.PutAsJsonAsync($"/api/v1/comments/{root}/like", new { userId = 1 });
        Assert.Equal(400, (int)forged.StatusCode);
        using var second = await client.PutAsync($"/api/v1/comments/{root}/like", null);
        Assert.Equal(204, (int)second.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var comments = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/reviews/{reviewId}/comments"));
        Assert.Equal(2, Assert.Single(comments.RootElement.EnumerateArray()).GetProperty("likeCount").GetInt32());
        using var anonymous = await client.PutAsync($"/api/v1/comments/{root}/like", null);
        Assert.Equal(401, (int)anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        using var missing = await client.PutAsync("/api/v1/comments/2147483647/like", null);
        Assert.Equal(404, (int)missing.StatusCode);
        using var invalid = await client.PutAsync("/api/v1/comments/0/like", null);
        Assert.Equal(400, (int)invalid.StatusCode);
        var deletions = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.DeleteAsync($"/api/v1/comments/{root}/like")));
        try { Assert.All(deletions, response => Assert.Equal(204, (int)response.StatusCode)); }
        finally { foreach (var response in deletions) response.Dispose(); }
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(1, await db.CommentLikes.CountAsync());
        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [PostgresFact]
    public async Task Notification_preparation_failure_rolls_back_SQL_comment_without_publishing_events()
    {
        await PostgresApiFactory.ResetAsync();
        await using var normal = new PostgresApiFactory();
        using var client = normal.CreateClient();
        var (userId, reviewId) = await Seed(client);
        await using var failedHost = normal.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped<INotifications>(provider => new FailingNotifications(new Tcc.Infrastructure.Persistence.Repositories.PostgresNotifications(provider.GetRequiredService<Lazy<TccDbContext>>())))));
        using var failedClient = failedHost.CreateClient();
        failedClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        using var failed = await failedClient.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/comments", new { text = "Must roll back" });
        Assert.Equal(500, (int)failed.StatusCode);
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(0, await db.Comments.CountAsync());
        Assert.Equal(0, await db.Notifications.CountAsync());
    }
    [Fact]
    public void Comments_and_likes_use_integer_relational_keys()
    {
        using var db = new TccDbContext(new DbContextOptionsBuilder<TccDbContext>().UseNpgsql(BootstrapTests.SyntheticConnection).Options);
        var comment = db.Model.FindEntityType(typeof(Comment));
        Assert.NotNull(comment);
        Assert.Equal(typeof(int), comment.FindProperty("Id")!.ClrType);
        Assert.Equal(typeof(int?), comment.FindProperty("ParentCommentId")!.ClrType);
        Assert.Contains(comment.GetForeignKeys(), key => key.Properties.Select(property => property.Name).SequenceEqual(new[] { "ReviewId", "ParentCommentId" }));
        var like = Assert.Single(db.Model.GetEntityTypes(), type => type.ClrType.Name == "CommentLike");
        Assert.Equal(new[] { "UserId", "CommentId" }, like.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.All(comment.GetForeignKeys().Concat(like.GetForeignKeys()), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Main_comment_reply_and_concurrent_likes_survive_restart()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Commenter", "commenter@example.com", "synthetic-password"));
        registered.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, "commenter@example.com", "synthetic-password");
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        using var review = await client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { title = "Fixture review", rating = 3, text = "Review" });
        review.EnsureSuccessStatusCode();
        using var reviewJson = JsonDocument.Parse(await review.Content.ReadAsStringAsync());
        var reviewId = reviewJson.RootElement.GetProperty("id").GetInt32();
        var path = $"/api/v1/reviews/{reviewId}/comments";
        using var main = await client.PostAsJsonAsync(path, new { text = "Main" });
        Assert.Equal(201, (int)main.StatusCode);
        using var mainJson = JsonDocument.Parse(await main.Content.ReadAsStringAsync());
        var mainId = mainJson.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(JsonValueKind.Null, mainJson.RootElement.GetProperty("parentCommentId").ValueKind);
        using var reply = await client.PostAsJsonAsync(path, new { text = "Reply", parentCommentId = mainId });
        Assert.Equal(201, (int)reply.StatusCode);
        using var replyJson = JsonDocument.Parse(await reply.Content.ReadAsStringAsync());
        var replyId = replyJson.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(mainId, replyJson.RootElement.GetProperty("parentCommentId").GetInt32());
        using var nested = await client.PostAsJsonAsync(path, new { text = "Second level", parentCommentId = replyId });
        Assert.Equal(400, (int)nested.StatusCode);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PutAsync($"/api/v1/comments/{replyId}/like", null)));
        try { Assert.All(responses, response => Assert.Equal(204, (int)response.StatusCode)); }
        finally { foreach (var response in responses) response.Dispose(); }
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        using var restored = JsonDocument.Parse(await other.GetStringAsync(path));
        Assert.Equal(2, restored.RootElement.GetArrayLength());
        var savedReply = Assert.Single(restored.RootElement.EnumerateArray(), item => item.GetProperty("id").GetInt32() == replyId);
        Assert.Equal(1, savedReply.GetProperty("likeCount").GetInt32());
        other.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        using var status = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/comments/{replyId}/like/me"));
        Assert.True(status.RootElement.GetProperty("likedByMe").GetBoolean());
        for (var index = 0; index < 2; index++)
        {
            using var removed = await other.DeleteAsync($"/api/v1/comments/{replyId}/like");
            Assert.Equal(204, (int)removed.StatusCode);
        }
        using var after = JsonDocument.Parse(await other.GetStringAsync(path));
        Assert.All(after.RootElement.EnumerateArray(), item => Assert.Equal(0, item.GetProperty("likeCount").GetInt32()));
    }
}
