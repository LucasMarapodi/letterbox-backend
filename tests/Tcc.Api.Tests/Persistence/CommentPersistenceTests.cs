using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcc.Api.Tests.Identity;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class CommentPersistenceTests
{
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
        using var review = await client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { text = "Review" });
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
        Assert.True(status.RootElement.GetProperty("liked").GetBoolean());
        for (var index = 0; index < 2; index++)
        {
            using var removed = await other.DeleteAsync($"/api/v1/comments/{replyId}/like");
            Assert.Equal(204, (int)removed.StatusCode);
        }
        using var after = JsonDocument.Parse(await other.GetStringAsync(path));
        Assert.All(after.RootElement.EnumerateArray(), item => Assert.Equal(0, item.GetProperty("likeCount").GetInt32()));
    }
}
