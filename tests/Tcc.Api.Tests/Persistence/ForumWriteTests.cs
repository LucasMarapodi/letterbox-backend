using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class ForumWriteTests
{
    [Theory]
    [InlineData("/api/v1/games/1/forum/topics")]
    [InlineData("/api/v1/forum/topics/1/replies")]
    public async Task Visitor_cannot_write_even_without_database(string path)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var response = await host.CreateClient().PostAsJsonAsync(path, new { text = "Reply", title = "Topic" });
        Assert.Equal(401, (int)response.StatusCode);
    }

    [PostgresFact]
    public async Task Session_creates_multiple_topics_and_concurrent_replies_without_creating_reviews_or_notifications()
    {
        await PostgresApiFactory.ResetAsync();
        var game = (await ProfileCompletionTests.Games())[0];
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "forum-owner@example.com");
        var reader = await NotificationPersistenceTests.Register(client, "forum-reader@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);
        var path = $"/api/v1/games/{game}/forum/topics";
        using var created = await client.PostAsJsonAsync(path, new { title = "  First topic  ", text = "  Initial message  " });
        Assert.Equal(201, (int)created.StatusCode);
        using var topic = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = topic.RootElement.GetProperty("id").GetInt32();
        Assert.Equal("/api/v1/forum/topics/" + id, created.Headers.Location?.ToString());
        Assert.Equal("First topic", topic.RootElement.GetProperty("title").GetString());
        Assert.Equal("Initial message", topic.RootElement.GetProperty("text").GetString());
        Assert.Equal(owner.Id, topic.RootElement.GetProperty("author").GetProperty("id").GetInt32());
        Assert.Equal(0, topic.RootElement.GetProperty("replyCount").GetInt32());
        using var second = await client.PostAsJsonAsync(path, new { title = new string('t', 150), text = new string('m', 10000) });
        Assert.Equal(201, (int)second.StatusCode);
        foreach (var input in new[] { new { title = "", text = "Text" }, new { title = "Topic", text = " \t " },
                     new { title = new string('t', 151), text = "Text" }, new { title = "Topic", text = new string('m', 10001) } })
        { using var invalid = await client.PostAsJsonAsync(path, input); Assert.Equal(400, (int)invalid.StatusCode); }
        foreach (var property in new[] { "userId", "reviewId", "rating", "parentReplyId" })
        {
            using var spoofed = await client.PostAsJsonAsync(path, new Dictionary<string, object> { ["title"] = "Topic", ["text"] = "Text", [property] = reader.Id });
            Assert.Equal(400, (int)spoofed.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", reader.Token);
        var repliesPath = $"/api/v1/forum/topics/{id}/replies";
        using var badReply = await client.PostAsJsonAsync(repliesPath, new { text = "Reply", parentReplyId = 1 });
        Assert.Equal(400, (int)badReply.StatusCode);
        using var emptyReply = await client.PostAsJsonAsync(repliesPath, new { text = " " });
        Assert.Equal(400, (int)emptyReply.StatusCode);
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => client.PostAsJsonAsync(repliesPath, new { text = "  Reply " + i + "  " })));
        var ids = new HashSet<int>();
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(201, (int)response.StatusCode); Assert.Equal(repliesPath, response.Headers.Location?.ToString());
                using var reply = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(id, reply.RootElement.GetProperty("topicId").GetInt32());
                Assert.Equal(reader.Id, reply.RootElement.GetProperty("author").GetProperty("id").GetInt32());
                Assert.True(ids.Add(reply.RootElement.GetProperty("id").GetInt32()));
            }
        }
        await using var restarted = new PostgresApiFactory();
        using var anonymous = restarted.CreateClient();
        using var restored = await ForumReadTests.Read(anonymous, $"/api/v1/forum/topics/{id}");
        Assert.Equal(12, restored.RootElement.GetProperty("replyCount").GetInt32());
        Assert.True(restored.RootElement.GetProperty("lastActivityAt").GetDateTimeOffset() >= restored.RootElement.GetProperty("createdAt").GetDateTimeOffset());
        using var page = await ForumReadTests.Read(anonymous, repliesPath);
        Assert.Equal(ids.Order(), page.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).Order());
        foreach (var missing in new[] { "/api/v1/games/2147483647/forum/topics", "/api/v1/forum/topics/2147483647/replies" })
        { using var response = await client.PostAsJsonAsync(missing, missing.Contains("games") ? new { text = "Reply", title = "Topic" } : (object)new { text = "Reply" }); Assert.Equal(404, (int)response.StatusCode); }
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(2, await db.ForumTopics.CountAsync()); Assert.Equal(12, await db.ForumReplies.CountAsync());
        Assert.Equal(0, await db.Reviews.CountAsync()); Assert.Equal(0, await db.Comments.CountAsync());
        Assert.Equal(0, await db.Ratings.CountAsync()); Assert.Equal(0, await db.Notifications.CountAsync());
        using var logout = await client.PostAsync("/api/v1/auth/logout", null); logout.EnsureSuccessStatusCode();
        using var revoked = await client.PostAsJsonAsync(repliesPath, new { text = "Must not persist" });
        Assert.Equal(401, (int)revoked.StatusCode); Assert.Equal(12, await db.ForumReplies.CountAsync());
    }
}
