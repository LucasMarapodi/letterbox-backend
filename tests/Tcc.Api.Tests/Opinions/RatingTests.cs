using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;

namespace Tcc.Api.Tests.Opinions;

public sealed class RatingTests
{
    [Fact]
    public async Task Ratings_accept_limits_and_decimal_replace_and_isolate_users()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var absence = await client.GetFromJsonAsync<MyRating>("/api/v1/games/igdb-1/rating/me");
        Assert.NotNull(absence);
        Assert.Null(absence.Rating);
        string? id = null;
        foreach (var value in new[] { 0d, 5d, 3.75d })
        {
            using var response = await client.PutAsJsonAsync("/api/v1/games/igdb-1/rating/me", new RatingInput(value));
            Assert.Equal(200, (int)response.StatusCode);
            var rating = await response.Content.ReadFromJsonAsync<RatingDto>();
            Assert.NotNull(rating);
            id ??= rating.Id;
            Assert.Equal(id, rating.Id);
            Assert.Equal(value, rating.Value);
            Assert.Equal("user-1", rating.UserId);
            Assert.Equal(rating, (await client.GetFromJsonAsync<MyRating>("/api/v1/games/igdb-1/rating/me"))?.Rating);
        }
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "p"));
        registration.EnsureSuccessStatusCode();
        await ApiTest.SignIn(client, "b@example.com", "p");
        Assert.Null((await client.GetFromJsonAsync<MyRating>("/api/v1/games/igdb-1/rating/me"))?.Rating);
    }

    [Theory]
    [InlineData("{\"value\":-0.1}")]
    [InlineData("{\"value\":5.1}")]
    [InlineData("{\"value\":\"NaN\"}")]
    [InlineData("{\"value\":1e999}")]
    [InlineData("{}")]
    [InlineData("{\"value\":3,\"userId\":\"other\"}")]
    public async Task Invalid_ratings_are_rejected_without_writing(string json)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        using var response = await ApiTest.Json(client, HttpMethod.Put, "/api/v1/games/igdb-1/rating/me", json);
        Assert.Equal(400, (int)response.StatusCode);
        Assert.Null((await client.GetFromJsonAsync<MyRating>("/api/v1/games/igdb-1/rating/me"))?.Rating);
    }

    [Fact]
    public async Task Personal_rating_requires_session_and_existing_game()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var get = await client.GetAsync("/api/v1/games/igdb-1/rating/me");
        using var put = await client.PutAsJsonAsync("/api/v1/games/igdb-1/rating/me", new RatingInput(3));
        Assert.Equal(401, (int)get.StatusCode);
        Assert.Equal(401, (int)put.StatusCode);
        await ApiTest.SignIn(client);
        using var missing = await client.PutAsJsonAsync("/api/v1/games/igdb-999/rating/me", new RatingInput(3));
        Assert.Equal(404, (int)missing.StatusCode);
    }
}
