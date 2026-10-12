using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;

namespace Tcc.Api.Tests.Opinions;

public sealed class ReviewQueriesTests
{
    [Fact]
    public async Task Reviews_are_public_filtered_sorted_and_do_not_expose_account_data()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var ana = await ApiTest.Publish(client);
        await ApiTest.Publish(client, "game-2");
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "p"));
        registration.EnsureSuccessStatusCode();
        await ApiTest.SignIn(client, "b@example.com", "p");
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var b = await ApiTest.Publish(client);
        Assert.Equal(b.Id, (await client.GetFromJsonAsync<MyReview>("/api/v1/games/game-1/reviews/me"))?.Review?.Id);
        Assert.Null((await client.GetFromJsonAsync<MyReview>("/api/v1/games/game-2/reviews/me"))?.Review);
        client.DefaultRequestHeaders.Authorization = null;
        var gameReviews = await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/games/game-1/reviews");
        Assert.NotNull(gameReviews);
        Assert.Equal(new[] { b.Id, ana.Id }, gameReviews.Select(review => review.Id));
        var userReviews = await client.GetFromJsonAsync<ReviewDto[]>($"/api/v1/users/{b.UserId}/reviews");
        Assert.NotNull(userReviews);
        Assert.Equal(b.Id, Assert.Single(userReviews).Id);
        using var response = await client.GetAsync($"/api/v1/reviews/{b.Id}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "avatarUrl", "displayName", "id" }, json.RootElement.GetProperty("author").EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("Ecos do Horizonte", b.Game.Title);
    }

    [Fact]
    public async Task Local_average_is_null_then_zero_then_changes_when_a_rating_is_replaced()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        Assert.Null((await client.GetFromJsonAsync<OpinionsDto>("/api/v1/games/game-1/opinions"))?.AverageRating);
        await ApiTest.SignIn(client);
        using var zero = await client.PutAsJsonAsync("/api/v1/games/game-1/rating/me", new RatingInput(0));
        zero.EnsureSuccessStatusCode();
        Assert.Equal(0d, (await client.GetFromJsonAsync<OpinionsDto>("/api/v1/games/game-1/opinions"))?.AverageRating);
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "p"));
        registration.EnsureSuccessStatusCode();
        await ApiTest.SignIn(client, "b@example.com", "p");
        using var five = await client.PutAsJsonAsync("/api/v1/games/game-1/rating/me", new RatingInput(5));
        five.EnsureSuccessStatusCode();
        Assert.Equal(2.5d, (await client.GetFromJsonAsync<OpinionsDto>("/api/v1/games/game-1/opinions"))?.AverageRating);
        await ApiTest.SignIn(client);
        using var four = await client.PutAsJsonAsync("/api/v1/games/game-1/rating/me", new RatingInput(4));
        four.EnsureSuccessStatusCode();
        Assert.Equal(4.5d, (await client.GetFromJsonAsync<OpinionsDto>("/api/v1/games/game-1/opinions"))?.AverageRating);
        Assert.Equal(4.5d, (await client.GetFromJsonAsync<GameDto>("/api/v1/games/game-1"))?.AverageRating);
    }

    [Theory]
    [InlineData("/api/v1/games/game-1/reviews/me", 401)]
    [InlineData("/api/v1/games/missing/reviews", 404)]
    [InlineData("/api/v1/games/missing/opinions", 404)]
    [InlineData("/api/v1/users/missing/reviews", 404)]
    [InlineData("/api/v1/reviews/missing", 404)]
    public async Task Missing_resources_and_private_queries_are_rejected(string url, int status)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.GetAsync(url);
        Assert.Equal(status, (int)response.StatusCode);
    }
}
