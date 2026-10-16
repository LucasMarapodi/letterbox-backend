using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Opinions;

namespace Tcc.Api.Tests.Social;

public sealed class FavoriteTests
{
    [Fact]
    public async Task Favorites_are_personal_separate_by_kind_and_idempotent_under_concurrency()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        await SocialTest.Register(client, "B");
        await ApiTest.SignIn(client);
        Assert.Null((await client.GetFromJsonAsync<FavoriteStatus>("/api/v1/favorites/game/igdb-1"))?.Favorite);
        var responses = await Task.WhenAll(client.PutAsync("/api/v1/favorites/game/igdb-1", null), client.PutAsync("/api/v1/favorites/game/igdb-1", null));
        try
        {
            Assert.All(responses, response => Assert.Equal(200, (int)response.StatusCode));
            var a = await responses[0].Content.ReadFromJsonAsync<FavoriteDto>();
            var b = await responses[1].Content.ReadFromJsonAsync<FavoriteDto>();
            Assert.Equal(a, b);
            Assert.Equal(1, a?.UserId);
            Assert.Equal("game", a?.TargetType);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        using var review = await client.PutAsync("/api/v1/favorites/review/review-fixture", null);
        review.EnsureSuccessStatusCode();
        var lists = await client.GetFromJsonAsync<FavoriteLists>("/api/v1/favorites?userId=other");
        Assert.NotNull(lists);
        Assert.Equal("igdb-1", Assert.Single(lists.Games).Id);
        Assert.Equal("review-fixture", Assert.Single(lists.Reviews).Id);
        await ApiTest.SignIn(client, "B@example.com", "p");
        var other = await client.GetFromJsonAsync<FavoriteLists>("/api/v1/favorites?userId=user-1");
        Assert.NotNull(other);
        Assert.Empty(other.Games);
        Assert.Empty(other.Reviews);
        await ApiTest.SignIn(client);
        for (var i = 0; i < 2; i++)
        {
            using var deleted = await client.DeleteAsync("/api/v1/favorites/game/igdb-1");
            Assert.Equal(204, (int)deleted.StatusCode);
        }
        Assert.Null((await client.GetFromJsonAsync<FavoriteStatus>("/api/v1/favorites/game/igdb-1"))?.Favorite);
        Assert.Empty((await client.GetFromJsonAsync<FavoriteLists>("/api/v1/favorites"))?.Games ?? []);
    }

    [Theory]
    [InlineData("wrong", "igdb-1", 400)]
    [InlineData("Game", "igdb-1", 400)]
    [InlineData("game", "igdb-999", 404)]
    [InlineData("review", "missing", 404)]
    public async Task Invalid_kind_or_missing_target_is_rejected_for_all_operations(string kind, string target, int expected)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var url = $"/api/v1/favorites/{kind}/{target}";
        using var get = await client.GetAsync(url);
        using var put = await client.PutAsync(url, null);
        using var delete = await client.DeleteAsync(url);
        Assert.Equal(expected, (int)get.StatusCode);
        Assert.Equal(expected, (int)put.StatusCode);
        Assert.Equal(expected, (int)delete.StatusCode);
    }

    [Fact]
    public async Task Anonymous_and_forged_commands_are_rejected()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var anonymous = await client.GetAsync("/api/v1/favorites");
        Assert.Equal(401, (int)anonymous.StatusCode);
        using var put = await client.PutAsync("/api/v1/favorites/game/igdb-1", null);
        Assert.Equal(401, (int)put.StatusCode);
        await ApiTest.SignIn(client);
        using var forged = await ApiTest.Json(client, HttpMethod.Put, "/api/v1/favorites/game/igdb-1", "{\"userId\":\"other\"}");
        Assert.Equal(400, (int)forged.StatusCode);
    }
}
