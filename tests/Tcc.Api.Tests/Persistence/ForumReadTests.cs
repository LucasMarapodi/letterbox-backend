using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Tcc.Domain.Forum;
using Tcc.Domain.Identity;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class ForumReadTests
{
    [Theory]
    [InlineData("/api/v1/games/0/forum/topics")]
    [InlineData("/api/v1/games/1/forum/topics?page=0")]
    [InlineData("/api/v1/games/1/forum/topics?page=2147483647")]
    [InlineData("/api/v1/forum/topics/-1")]
    [InlineData("/api/v1/forum/topics/1/replies?page=-1")]
    public async Task Invalid_query_fails_before_database_access(string path)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var response = await host.CreateClient().GetAsync(path);
        Assert.Equal(400, (int)response.StatusCode);
    }

    [PostgresFact]
    public async Task Public_queries_isolate_game_topic_and_page_activity_and_chronology_with_safe_authors()
    {
        await PostgresApiFactory.ResetAsync();
        var games = await ProfileCompletionTests.Games();
        await using var db = PostgresTestDatabase.Context();
        var now = DateTimeOffset.UtcNow;
        var author = new UserAccount(0, "", Email.Create("forum-read@example.com"), "synthetic", UserProfile.Create("Public author", ""), now);
        db.Users.Add(author); await db.SaveChangesAsync();
        var topics = Enumerable.Range(0, 21).Select(i => new ForumTopic(0, games[0], author.Id, "Topic " + i, "Initial text", now.AddDays(-2))).ToArray();
        var foreign = new ForumTopic(0, games[1], author.Id, "Other game", "Other text", now);
        db.ForumTopics.AddRange(topics); db.ForumTopics.Add(foreign); await db.SaveChangesAsync();
        var replies = Enumerable.Range(0, 21).Select(i => new ForumReply(0, topics[0].Id, author.Id, "Reply " + i, now.AddMinutes(i))).ToArray();
        db.ForumReplies.AddRange(replies);
        db.ForumReplies.Add(new(0, foreign.Id, author.Id, "Foreign reply", now));
        await db.SaveChangesAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        using var first = await Read(client, $"/api/v1/games/{games[0]}/forum/topics");
        var items = first.RootElement.GetProperty("items");
        Assert.Equal(20, items.GetArrayLength()); Assert.True(first.RootElement.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(topics[0].Id, items[0].GetProperty("id").GetInt32());
        Assert.Equal(topics[20].Id, items[1].GetProperty("id").GetInt32());
        Assert.Equal(21, items[0].GetProperty("replyCount").GetInt32());
        Assert.Equal(replies[20].CreatedAt, items[0].GetProperty("lastActivityAt").GetDateTimeOffset());
        Assert.Equal("Public author", items[0].GetProperty("author").GetProperty("displayName").GetString());
        Assert.Equal(author.Username, items[0].GetProperty("author").GetProperty("username").GetString());
        Assert.DoesNotContain("email", first.RootElement.GetRawText());
        Assert.DoesNotContain("senha", first.RootElement.GetRawText());
        Assert.False(items[0].TryGetProperty("text", out _));
        using var next = await Read(client, $"/api/v1/games/{games[0]}/forum/topics?page=2");
        Assert.Single(next.RootElement.GetProperty("items").EnumerateArray());
        using var detail = await Read(client, $"/api/v1/forum/topics/{topics[0].Id}");
        Assert.Equal("Initial text", detail.RootElement.GetProperty("text").GetString());
        Assert.Equal(games[0], detail.RootElement.GetProperty("game").GetProperty("id").GetInt32());
        using var page = await Read(client, $"/api/v1/forum/topics/{topics[0].Id}/replies");
        Assert.Equal(20, page.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal("Reply 0", page.RootElement.GetProperty("items")[0].GetProperty("text").GetString());
        using var last = await Read(client, $"/api/v1/forum/topics/{topics[0].Id}/replies?page=2");
        Assert.Equal("Reply 20", last.RootElement.GetProperty("items")[0].GetProperty("text").GetString());
        Assert.False(last.RootElement.GetProperty("hasNextPage").GetBoolean());
        using var empty = await Read(client, $"/api/v1/games/{games[2]}/forum/topics");
        Assert.Empty(empty.RootElement.GetProperty("items").EnumerateArray());
        foreach (var path in new[] { "/api/v1/games/2147483647/forum/topics", "/api/v1/forum/topics/2147483647", "/api/v1/forum/topics/2147483647/replies" })
        { using var missing = await client.GetAsync(path); Assert.Equal(404, (int)missing.StatusCode); }
    }

    internal static async Task<JsonDocument> Read(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(200, (int)response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
