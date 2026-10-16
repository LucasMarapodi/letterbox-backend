using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tcc.Application.Social;
using Tcc.Api.Tests.Social;
using Tcc.Infrastructure.Persistence.Repositories;
using Tcc.Api.Tests.Identity;
using Tcc.Domain.Social;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class NotificationPersistenceTests
{
    private static async Task<(int Game, (int Id, string Token) Author, (int Id, string Token) Reader)> Seed(HttpClient client)
    {
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        var author = await Register(client, "atomic-author@example.com");
        var reader = await Register(client, "atomic-reader@example.com");
        using var follow = await client.PutAsync($"/api/v1/users/{author.Id}/follow", null);
        follow.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        return (game.Id, author, reader);
    }
    private static async Task<int> Review(HttpClient client, int game)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new { title = "Fixture review", rating = 3, text = "Atomic review" });
        Assert.Equal(201, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    [PostgresFact]
    public async Task SQL_events_and_publications_rollback_together_after_notification_insertion_failure()
    {
        await PostgresApiFactory.ResetAsync();
        await using var normal = new PostgresApiFactory();
        using var client = normal.CreateClient();
        var setup = await Seed(client);
        await using var failing = normal.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped<INotifications>(provider => new FailingNotifications(new PostgresNotifications(provider.GetRequiredService<Lazy<TccDbContext>>())))));
        using var failed = failing.CreateClient();
        failed.DefaultRequestHeaders.Authorization = new("Bearer", setup.Author.Token);
        using var failedReview = await failed.PostAsJsonAsync($"/api/v1/games/{setup.Game}/reviews", new { title = "Fixture review", rating = 3, text = "Rollback review" });
        Assert.Equal(500, (int)failedReview.StatusCode);
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(0, await db.Reviews.CountAsync());
        Assert.Equal(0, await db.Notifications.CountAsync());
        var reviewId = await Review(client, setup.Game);
        Assert.Equal(1, await db.Notifications.CountAsync());
        failed.DefaultRequestHeaders.Authorization = new("Bearer", setup.Reader.Token);
        using var failedComment = await failed.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/comments", new { text = "Rollback comment" });
        Assert.Equal(500, (int)failedComment.StatusCode);
        Assert.Equal(0, await db.Comments.CountAsync());
        Assert.Equal(1, await db.Reviews.CountAsync());
        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [PostgresFact]
    public async Task Other_requests_cannot_observe_review_or_events_before_transaction_commit()
    {
        await PostgresApiFactory.ResetAsync();
        var gate = new NotificationGate();
        await using var original = new PostgresApiFactory();
        await using var host = original.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped<INotifications>(provider => new PausedNotifications(new PostgresNotifications(provider.GetRequiredService<Lazy<TccDbContext>>()), gate))));
        using var client = host.CreateClient();
        var setup = await Seed(client);
        var pending = client.PostAsJsonAsync($"/api/v1/games/{setup.Game}/reviews", new { title = "Fixture review", rating = 3, text = "Not yet visible" });
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var reader = host.CreateClient();
            reader.DefaultRequestHeaders.Authorization = new("Bearer", setup.Reader.Token);
            using var events = JsonDocument.Parse(await reader.GetStringAsync("/api/v1/notifications").WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(0, events.RootElement.GetArrayLength());
            using var reviews = JsonDocument.Parse(await reader.GetStringAsync($"/api/v1/games/{setup.Game}/reviews").WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(0, reviews.RootElement.GetArrayLength());
        }
        finally { gate.Release.TrySetResult(); }
        using var response = await pending;
        Assert.Equal(201, (int)response.StatusCode);
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(1, await db.Reviews.CountAsync());
        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [PostgresFact]
    public async Task SQL_enforces_event_shape_comment_review_membership_and_deduplicates_retries()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var setup = await Seed(client);
        var reviewId = await Review(client, setup.Game);
        using var self = await client.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/comments", new { text = "Self comment" });
        self.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", setup.Reader.Token);
        using var comment = await client.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/comments", new { text = "Comment" });
        comment.EnsureSuccessStatusCode();
        using var commentJson = JsonDocument.Parse(await comment.Content.ReadAsStringAsync());
        var commentId = commentJson.RootElement.GetProperty("id").GetInt32();
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(2, await db.Notifications.CountAsync());
        var original = await db.Notifications.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var writer = new PostgresNotifications(new Lazy<TccDbContext>(() => db));
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await writer.AddRangeAsync([Notification.NewReview(setup.Reader.Id, setup.Author.Id, reviewId, DateTimeOffset.UtcNow),
                Notification.ReviewComment(setup.Author.Id, setup.Reader.Id, reviewId, commentId, DateTimeOffset.UtcNow)]);
            await tx.CommitAsync();
        }
        Assert.Equal(original, await db.Notifications.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync());
        foreach (var type in new[] { "unknown", "review-comment" })
        {
            var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO notificacoes(destinatario_id,ator_id,review_id,tipo,criado_em) VALUES({setup.Reader.Id},{setup.Author.Id},{reviewId},{type},now())"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        }
        var own = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO notificacoes(destinatario_id,ator_id,review_id,tipo,criado_em) VALUES({setup.Author.Id},{setup.Author.Id},{reviewId},{"new-review"},now())"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, own.SqlState);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO notificacoes(destinatario_id,ator_id,review_id,tipo,criado_em) VALUES({setup.Reader.Id},{setup.Author.Id},{reviewId},{"new-review"},now())"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload(4_000_000_001)], default)).Game;
        db.Reviews.Add(Tcc.Domain.Opinions.Review.Create(0, setup.Author.Id, game.Id, "Other review", DateTimeOffset.UtcNow, "Fixture review"));
        await db.SaveChangesAsync();
        var other = await db.Reviews.SingleAsync(item => item.GameId == game.Id && item.UserId == setup.Author.Id);
        var third = await Register(client, "coherence-recipient@example.com");
        var cross = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO notificacoes(destinatario_id,ator_id,review_id,comentario_id,tipo,criado_em) VALUES({third.Id},{setup.Reader.Id},{other.Id},{commentId},{"review-comment"},now())"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, cross.SqlState);
    }
    [Fact]
    public void Notification_model_has_integer_identity_coherent_comment_FK_and_event_uniqueness()
    {
        using var db = new TccDbContext(new DbContextOptionsBuilder<TccDbContext>().UseNpgsql(BootstrapTests.SyntheticConnection).Options);
        var model = db.Model.FindEntityType(typeof(Notification));
        Assert.NotNull(model);
        Assert.Equal(typeof(int), model.FindProperty("Id")!.ClrType);
        Assert.Equal(typeof(int?), model.FindProperty("CommentId")!.ClrType);
        Assert.Equal(4, model.GetForeignKeys().Count());
        Assert.All(model.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
        Assert.Contains(model.GetForeignKeys(), key => key.Properties.Select(property => property.Name).SequenceEqual(new[] { "ReviewId", "CommentId" }));
        Assert.Equal(2, model.GetIndexes().Count(index => index.IsUnique && index.GetFilter() is not null));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    internal static async Task<(int Id, string Token)> Register(HttpClient client, string email)
    {
        using var result = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Notification user", email, "synthetic-password"));
        result.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, email, "synthetic-password");
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        return (login.User.Id, login.Token);
    }

    [PostgresFact]
    public async Task Concurrent_review_creates_one_event_per_recipient_and_notifications_survive_restart()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var author = await Register(client, "notification-author@example.com");
        var reader = await Register(client, "notification-reader@example.com");
        using var followed = await client.PutAsync($"/api/v1/users/{author.Id}/follow", null);
        followed.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { title = "Fixture review", rating = 3, text = "Atomic review" })));
        try { Assert.Single(results, result => (int)result.StatusCode == 201); Assert.Equal(7, results.Count(result => (int)result.StatusCode == 409)); }
        finally { foreach (var result in results) result.Dispose(); }
        client.DefaultRequestHeaders.Authorization = new("Bearer", reader.Token);
        using var original = JsonDocument.Parse(await client.GetStringAsync("/api/v1/notifications"));
        var originalEvent = Assert.Single(original.RootElement.EnumerateArray());
        var id = originalEvent.GetProperty("id").GetInt32();
        var reviewId = originalEvent.GetProperty("reviewId").GetInt32();
        using var comment = await client.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/comments", new { text = "Atomic comment" });
        Assert.Equal(201, (int)comment.StatusCode);
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        other.DefaultRequestHeaders.Authorization = new("Bearer", reader.Token);
        using var restored = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/notifications?userId={author.Id}"));
        Assert.Equal(id, Assert.Single(restored.RootElement.EnumerateArray()).GetProperty("id").GetInt32());
        other.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        using var authorEvents = JsonDocument.Parse(await other.GetStringAsync("/api/v1/notifications"));
        var item = Assert.Single(authorEvents.RootElement.EnumerateArray());
        Assert.Equal("review-comment", item.GetProperty("type").GetString());
        Assert.Equal(reader.Id, item.GetProperty("actorId").GetInt32());
        Assert.True(item.GetProperty("commentId").GetInt32() > 0);
        Assert.Equal(2, await db.Set<Notification>().CountAsync());
    }
}

internal sealed class NotificationGate
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
internal sealed class PausedNotifications(INotifications inner, NotificationGate gate) : INotifications
{
    public async Task AddRangeAsync(IReadOnlyList<Notification> events, CancellationToken ct = default)
    {
        await inner.AddRangeAsync(events, ct);
        gate.Entered.TrySetResult();
        await gate.Release.Task.WaitAsync(ct);
    }
    public Task<IReadOnlyList<Notification>> ForUserAsync(int userId, CancellationToken ct = default) => inner.ForUserAsync(userId, ct);
}
