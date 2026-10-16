using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Tests.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Social;
using Tcc.Infrastructure.Social;

namespace Tcc.Api.Tests.Social;

public sealed class PublicationFailureTests
{
    [Fact]
    public async Task Notification_failure_rolls_back_review_comment_and_partial_events()
    {
        await using var host = new FailingNotificationFactory();
        using var client = host.CreateClient();
        var b = await SocialTest.Register(client, "B");
        await ApiTest.SignIn(client, "B@example.com", "p");
        await SocialTest.Follow(client, "user-1");
        await ApiTest.SignIn(client);
        using var failedReview = await client.PostAsJsonAsync("/api/v1/games/game-1/reviews", new TextInput("Review"));
        Assert.Equal(500, (int)failedReview.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/games/game-1/reviews")) ?? []);
        Assert.Empty(host.Services.GetRequiredService<INotifications>().ForUser(b.Id));
        await ApiTest.SignIn(client, "B@example.com", "p");
        using var failedComment = await client.PostAsJsonAsync("/api/v1/reviews/review-fixture/comments", new TextInput("Comment"));
        Assert.Equal(500, (int)failedComment.StatusCode);
        Assert.Equal(3, (await client.GetFromJsonAsync<CommentDto[]>("/api/v1/reviews/review-fixture/comments"))?.Length);
        Assert.Empty(host.Services.GetRequiredService<INotifications>().ForUser("user-1"));
    }
}

public sealed class FailingNotificationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        services.AddSingleton<INotifications>(provider => new FailingNotifications(
            new InMemoryNotifications(provider.GetRequiredService<InMemoryPublicationScope>()))));
}

public sealed class FailingNotifications(INotifications inner) : INotifications
{
    public void AddRange(IReadOnlyList<Notification> notifications)
    {
        inner.AddRange(notifications);
        throw new InvalidOperationException("Falha simulada de gravação.");
    }
    public IReadOnlyList<Notification> ForUser(string userId) => inner.ForUser(userId);
}
