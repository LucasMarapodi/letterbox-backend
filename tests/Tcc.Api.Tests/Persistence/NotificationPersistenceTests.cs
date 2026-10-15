using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcc.Api.Tests.Identity;
using Tcc.Domain.Social;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class NotificationPersistenceTests
{
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
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { text = "Atomic review" })));
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
