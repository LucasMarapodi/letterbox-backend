using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;

namespace Tcc.Api.Tests.Opinions;

public sealed class PublishReviewTests
{
    [Fact]
    public async Task Publish_uses_session_clock_and_location_and_is_readable()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        using var response = await client.PostAsJsonAsync("/api/v1/games/game-1/reviews", new TextInput("  Good game  "));
        Assert.Equal(201, (int)response.StatusCode);
        var review = await response.Content.ReadFromJsonAsync<ReviewDto>();
        Assert.NotNull(review);
        Assert.Equal("user-1", review.UserId);
        Assert.Equal("game-1", review.GameId);
        Assert.Equal("Good game", review.Text);
        Assert.Equal(clock.GetUtcNow(), review.CreatedAt);
        Assert.Null(review.UpdatedAt);
        Assert.Equal($"/api/v1/reviews/{review.Id}", response.Headers.Location?.OriginalString);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(review, await client.GetFromJsonAsync<ReviewDto>($"/api/v1/reviews/{review.Id}"));
    }

    [Fact]
    public async Task Concurrent_publication_keeps_only_one_review_per_user_game()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/games/game-1/reviews", new TextInput("One")),
            client.PostAsJsonAsync("/api/v1/games/game-1/reviews", new TextInput("Two")));
        try
        {
            Assert.Single(responses, response => (int)response.StatusCode == 201);
            Assert.Single(responses, response => (int)response.StatusCode == 409);
            using var duplicate = await client.PostAsJsonAsync("/api/v1/games/game-1/reviews", new TextInput("Three"));
            Assert.Equal(409, (int)duplicate.StatusCode);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData("{\"text\":\" \"}")]
    [InlineData("{\"text\":null}")]
    [InlineData("{\"text\":\"Good\",\"userId\":\"other\"}")]
    public async Task Invalid_text_or_author_forgery_is_rejected(string json)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        using var response = await ApiTest.Json(client, HttpMethod.Post, "/api/v1/games/game-1/reviews", json);
        Assert.Equal(400, (int)response.StatusCode);
    }

    [Fact]
    public async Task Review_publication_requires_session_and_game()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var anonymous = await client.PostAsJsonAsync("/api/v1/games/game-1/reviews", new TextInput("Good"));
        Assert.Equal(401, (int)anonymous.StatusCode);
        await ApiTest.SignIn(client);
        using var missing = await client.PostAsJsonAsync("/api/v1/games/missing/reviews", new TextInput("Good"));
        Assert.Equal(404, (int)missing.StatusCode);
    }
}
