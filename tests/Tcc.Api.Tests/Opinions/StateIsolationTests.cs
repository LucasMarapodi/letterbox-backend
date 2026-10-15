using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Opinions;

public sealed class StateIsolationTests
{
    [Fact]
    public async Task Writes_survive_requests_in_one_host_and_new_host_restores_only_fixtures()
    {
        await using var first = new TestApiFactory();
        await using var restarted = new TestApiFactory();
        using var client = first.CreateClient();
        using var other = restarted.CreateClient();
        await ApiTest.SignIn(client);
        using var rating = await client.PutAsJsonAsync("/api/v1/games/1/rating/me", new RatingInput(4));
        rating.EnsureSuccessStatusCode();
        var review = await ApiTest.Publish(client);
        using var comment = await client.PostAsJsonAsync("/api/v1/reviews/review-fixture/comments", new TextInput("New"));
        comment.EnsureSuccessStatusCode();
        Assert.Equal(4d, (await client.GetFromJsonAsync<GameDto>("/api/v1/games/1"))?.AverageRating);
        Assert.Equal(4, (await client.GetFromJsonAsync<CommentDto[]>("/api/v1/reviews/review-fixture/comments"))?.Length);
        Assert.Null((await other.GetFromJsonAsync<GameDto>("/api/v1/games/1"))?.AverageRating);
        Assert.Empty((await other.GetFromJsonAsync<ReviewDto[]>("/api/v1/games/1/reviews")) ?? []);
        Assert.Equal(3, (await other.GetFromJsonAsync<CommentDto[]>("/api/v1/reviews/review-fixture/comments"))?.Length);
        using var missing = await other.GetAsync($"/api/v1/reviews/{review.Id}");
        Assert.Equal(404, (int)missing.StatusCode);
        Assert.Equal(review.Id, (await client.GetFromJsonAsync<ReviewDto>($"/api/v1/reviews/{review.Id}"))?.Id);
    }
}
