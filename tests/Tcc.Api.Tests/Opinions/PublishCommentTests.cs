using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;

namespace Tcc.Api.Tests.Opinions;

public sealed class PublishCommentTests
{
    [Fact]
    public async Task Own_and_other_users_comments_are_published_with_session_identity_and_clock()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var review = await ApiTest.Publish(client);
        var url = $"/api/v1/reviews/{review.Id}/comments";
        using var own = await client.PostAsJsonAsync(url, new TextInput("  Own comment  "));
        Assert.Equal(201, (int)own.StatusCode);
        var first = await own.Content.ReadFromJsonAsync<CommentDto>();
        Assert.NotNull(first);
        Assert.Equal(1, first.UserId);
        Assert.Equal(review.Id, first.ReviewId);
        Assert.Equal("Own comment", first.Text);
        Assert.Equal(clock.GetUtcNow(), first.CreatedAt);
        Assert.Equal(url, own.Headers.Location?.OriginalString);
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "p"));
        registration.EnsureSuccessStatusCode();
        await ApiTest.SignIn(client, "b@example.com", "p");
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        using var other = await client.PostAsJsonAsync(url, new TextInput("Other comment"));
        Assert.Equal(201, (int)other.StatusCode);
        var second = await other.Content.ReadFromJsonAsync<CommentDto>();
        Assert.NotNull(second);
        Assert.NotEqual(first.UserId, second.UserId);
        Assert.Equal("B", second.Author.DisplayName);
        client.DefaultRequestHeaders.Authorization = null;
        var comments = await client.GetFromJsonAsync<CommentDto[]>(url);
        Assert.NotNull(comments);
        Assert.Equal(new[] { first, second }, comments);
    }

    [Theory]
    [InlineData("{\"text\":\" \"}")]
    [InlineData("{\"text\":null}")]
    [InlineData("{\"text\":\"Good\",\"userId\":\"other\"}")]
    [InlineData("{\"text\":\"Good\",\"parentCommentId\":\"other\"}")]
    public async Task Invalid_comment_is_rejected_without_writing(string json)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var review = await ApiTest.Publish(client);
        var url = $"/api/v1/reviews/{review.Id}/comments";
        using var response = await ApiTest.Json(client, HttpMethod.Post, url, json);
        Assert.Equal(400, (int)response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<CommentDto[]>(url)) ?? []);
    }

    [Fact]
    public async Task Missing_review_anonymous_expired_and_logged_out_sessions_cannot_write()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        const string url = "/api/v1/reviews/review-fixture/comments";
        using var anonymous = await client.PostAsJsonAsync(url, new TextInput("Good"));
        Assert.Equal(401, (int)anonymous.StatusCode);
        await ApiTest.SignIn(client);
        using var missing = await client.PostAsJsonAsync("/api/v1/reviews/missing/comments", new TextInput("Good"));
        Assert.Equal(404, (int)missing.StatusCode);
        using var logout = await client.PostAsync("/api/v1/auth/logout", null);
        logout.EnsureSuccessStatusCode();
        using var revoked = await client.PostAsJsonAsync(url, new TextInput("Good"));
        Assert.Equal(401, (int)revoked.StatusCode);
        await ApiTest.SignIn(client);
        clock.UtcNow = clock.UtcNow.AddMinutes(60);
        using var expired = await client.PostAsJsonAsync(url, new TextInput("Good"));
        Assert.Equal(401, (int)expired.StatusCode);
        Assert.Equal(3, (await client.GetFromJsonAsync<CommentDto[]>(url))?.Length);
    }
}
