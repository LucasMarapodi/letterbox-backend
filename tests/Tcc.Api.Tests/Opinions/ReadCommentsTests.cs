using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Opinions;

public sealed class ReadCommentsTests
{
    [Fact]
    public async Task Visitors_read_flat_comments_sorted_by_date_then_id_with_safe_authors()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        var comments = await client.GetFromJsonAsync<CommentDto[]>("/api/v1/reviews/review-fixture/comments");
        Assert.NotNull(comments);
        Assert.Equal(new[] { "comment-c", "comment-a", "comment-b" }, comments.Select(comment => comment.Id));
        Assert.All(comments, comment => Assert.Equal("review-fixture", comment.ReviewId));
        using var response = await client.GetAsync("/api/v1/reviews/review-fixture/comments");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "avatarUrl", "displayName", "id" }, json.RootElement[0].GetProperty("author").EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(new[] { "author", "createdAt", "id", "reviewId", "text", "userId" }, json.RootElement[0].EnumerateObject().Select(property => property.Name).Order());
    }

    [Fact]
    public async Task Empty_review_does_not_receive_other_review_comments_and_missing_review_returns_404()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var review = await ApiTest.Publish(client);
        client.DefaultRequestHeaders.Authorization = null;
        var comments = await client.GetFromJsonAsync<CommentDto[]>($"/api/v1/reviews/{review.Id}/comments");
        Assert.NotNull(comments);
        Assert.Empty(comments);
        using var missing = await client.GetAsync("/api/v1/reviews/missing/comments");
        Assert.Equal(404, (int)missing.StatusCode);
    }
}
