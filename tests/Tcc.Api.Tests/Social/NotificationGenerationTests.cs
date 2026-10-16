using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Social;

namespace Tcc.Api.Tests.Social;

public sealed class NotificationGenerationTests
{
    private static IReadOnlyList<Notification> Events(WebApplicationFactory<Program> host, string userId) =>
        host.Services.GetService<INotifications>()?.ForUser(userId) ?? [];

    [Fact]
    public async Task Successful_review_notifies_only_current_followers_once_even_with_concurrent_duplicate()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        var b = await SocialTest.Register(client, "B");
        var c = await SocialTest.Register(client, "C");
        var d = await SocialTest.Register(client, "D");
        await ApiTest.SignIn(client, "B@example.com", "p");
        await SocialTest.Follow(client, "user-1");
        await ApiTest.SignIn(client, "C@example.com", "p");
        await SocialTest.Follow(client, "user-1");
        await ApiTest.SignIn(client);
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/games/igdb-1/reviews", new TextInput("One")),
            client.PostAsJsonAsync("/api/v1/games/igdb-1/reviews", new TextInput("Two")));
        ReviewDto review;
        try
        {
            var created = Assert.Single(responses, response => (int)response.StatusCode == 201);
            Assert.Single(responses, response => (int)response.StatusCode == 409);
            review = (await created.Content.ReadFromJsonAsync<ReviewDto>()) ?? throw new InvalidOperationException();
        }
        finally { foreach (var response in responses) response.Dispose(); }
        foreach (var recipient in new[] { b, c })
        {
            var notification = Assert.Single(Events(host, recipient.Id));
            Assert.Equal(NotificationType.NewReview, notification.Type);
            Assert.Equal(review.Id, notification.ReviewId);
            Assert.Equal("user-1", notification.ActorId);
            Assert.Equal(clock.GetUtcNow(), notification.CreatedAt);
            Assert.Null(notification.CommentId);
        }
        Assert.Empty(Events(host, d.Id));
        Assert.Empty(Events(host, "user-1"));
        using var edit = await client.PatchAsJsonAsync($"/api/v1/reviews/{review.Id}", new TextInput("Edited"));
        edit.EnsureSuccessStatusCode();
        Assert.Single(Events(host, b.Id));
        await ApiTest.SignIn(client, "B@example.com", "p");
        using var unfollow = await client.DeleteAsync("/api/v1/users/user-1/follow");
        unfollow.EnsureSuccessStatusCode();
        await ApiTest.SignIn(client);
        await ApiTest.Publish(client, "igdb-2");
        Assert.Single(Events(host, b.Id));
        Assert.Equal(2, Events(host, c.Id).Count);
    }

    [Fact]
    public async Task Comment_notifies_only_review_author_and_self_comment_or_failures_do_not_notify()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        var b = await SocialTest.Register(client, "B");
        await ApiTest.SignIn(client);
        var review = await ApiTest.Publish(client);
        var url = $"/api/v1/reviews/{review.Id}/comments";
        using var own = await client.PostAsJsonAsync(url, new TextInput("Own"));
        own.EnsureSuccessStatusCode();
        Assert.Empty(Events(host, "user-1"));
        await ApiTest.SignIn(client, "B@example.com", "p");
        using var response = await client.PostAsJsonAsync(url, new TextInput("Other"));
        response.EnsureSuccessStatusCode();
        var comment = await response.Content.ReadFromJsonAsync<CommentDto>();
        var notification = Assert.Single(Events(host, "user-1"));
        Assert.Equal(NotificationType.ReviewComment, notification.Type);
        Assert.Equal(comment?.Id, notification.CommentId);
        Assert.Equal(comment?.CreatedAt, notification.CreatedAt);
        Assert.Equal(b.Id, notification.ActorId);
        Assert.Empty(Events(host, b.Id));
        using var invalid = await client.PostAsJsonAsync(url, new TextInput(" "));
        Assert.Equal(400, (int)invalid.StatusCode);
        using var missing = await client.PostAsJsonAsync("/api/v1/reviews/missing/comments", new TextInput("Text"));
        Assert.Equal(404, (int)missing.StatusCode);
        Assert.Single(Events(host, "user-1"));
        host.Services.GetRequiredService<INotifications>().AddRange([notification, notification]);
        Assert.Single(Events(host, "user-1"));
    }
}
