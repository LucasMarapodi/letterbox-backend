using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;

namespace Tcc.Api.Tests.Social;

public sealed class NotificationQueryTests
{
    [Fact]
    public async Task Notification_list_requires_session_and_empty_list_is_valid()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var anonymous = await client.GetAsync("/api/v1/notifications");
        Assert.Equal(401, (int)anonymous.StatusCode);
        await ApiTest.SignIn(client);
        var items = await client.GetFromJsonAsync<NotificationDto[]>("/api/v1/notifications");
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task List_is_private_complete_sorted_and_compatible_with_notification_links()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        var b = await SocialTest.Register(client, "B");
        await SocialTest.Register(client, "C");
        await ApiTest.SignIn(client, "B@example.com", "p");
        await SocialTest.Follow(client, 1);
        var bReview = await ApiTest.Publish(client);
        await ApiTest.SignIn(client);
        var first = await ApiTest.Publish(client);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        using var commentResponse = await client.PostAsJsonAsync($"/api/v1/reviews/{bReview.Id}/comments", new TextInput("Comment"));
        commentResponse.EnsureSuccessStatusCode();
        var comment = await commentResponse.Content.ReadFromJsonAsync<CommentDto>();
        var last = await ApiTest.Publish(client, 2);
        await ApiTest.SignIn(client, "B@example.com", "p");
        var items = await client.GetFromJsonAsync<NotificationDto[]>("/api/v1/notifications?userId=user-1");
        Assert.NotNull(items);
        Assert.Equal(3, items.Length);
        Assert.All(items, item => Assert.Equal(b.Id, item.UserId));
        Assert.Equal(items.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).Select(item => item.Id), items.Select(item => item.Id));
        Assert.Equal(first.Id, items[^1].ReviewId);
        Assert.Contains(items, item => item.Type == "new-review" && item.ReviewId == last.Id && item.CommentId is null);
        Assert.Contains(items, item => item.Type == "review-comment" && item.ReviewId == bReview.Id && item.CommentId == comment?.Id);
        foreach (var item in items)
        {
            using var review = await client.GetAsync($"/api/v1/reviews/{item.ReviewId}");
            review.EnsureSuccessStatusCode();
        }
        using var response = await client.GetAsync("/api/v1/notifications");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var item in json.RootElement.EnumerateArray())
        {
            Assert.Equal(new[] { "avatarUrl", "displayName", "id" }, item.GetProperty("actor").EnumerateObject().Select(property => property.Name).Order());
            Assert.False(item.TryGetProperty("read", out _));
            if (item.GetProperty("type").GetString() == "new-review") Assert.False(item.TryGetProperty("commentId", out _));
        }
        await ApiTest.SignIn(client, "C@example.com", "p");
        Assert.Empty((await client.GetFromJsonAsync<NotificationDto[]>($"/api/v1/notifications?userId={b.Id}")) ?? []);
    }
}
