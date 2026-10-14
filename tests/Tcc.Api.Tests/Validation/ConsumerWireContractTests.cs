using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Opinions;

namespace Tcc.Api.Tests.Validation;

public sealed class ConsumerWireContractTests
{
    [Theory]
    [InlineData("/api/v1/games/igdb-1/rating/me", "rating")]
    [InlineData("/api/v1/games/igdb-1/reviews/me", "review")]
    [InlineData("/api/v1/favorites/game/igdb-1", "favorite")]
    public async Task Missing_personal_value_is_explicit_null_in_a_camel_case_wrapper(string path, string property)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        using var response = await client.GetAsync(path);
        Assert.Equal(200, (int)response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(property, Assert.Single(document.RootElement.EnumerateObject()).Name);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty(property).ValueKind);
    }

    [Theory]
    [InlineData("/api/v1/users/user-1/follow", "/api/v1/users/user-1/follow/me", "following")]
    [InlineData("/api/v1/favorites/game/igdb-1", "/api/v1/favorites/game/igdb-1", "favorite")]
    public async Task Delete_returns_zero_bytes_and_personal_lookup_confirms_absence(string path, string lookup, string property)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "Consumer", email = "consumer@example.com", password = "p" });
        Assert.Equal(201, (int)registered.StatusCode);
        await ApiTest.SignIn(client, "consumer@example.com", "p");
        using var added = await client.PutAsync(path, null);
        Assert.Equal(200, (int)added.StatusCode);
        using var deleted = await client.DeleteAsync(path);
        Assert.Equal(204, (int)deleted.StatusCode);
        Assert.Empty(await deleted.Content.ReadAsByteArrayAsync());
        Assert.Null(deleted.Content.Headers.ContentType);
        using var response = await client.GetAsync(lookup);
        Assert.Equal(200, (int)response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actual = document.RootElement.GetProperty(property);
        Assert.Equal(property == "following" ? JsonValueKind.False : JsonValueKind.Null, actual.ValueKind);
    }

    [Fact]
    public async Task Real_review_and_game_expose_camel_case_null_and_ISO_dates()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        using var published = await client.PostAsJsonAsync("/api/v1/games/igdb-1/reviews", new TextInput("Consumer contract"));
        Assert.Equal(201, (int)published.StatusCode);
        using var review = JsonDocument.Parse(await published.Content.ReadAsStringAsync());
        var root = review.RootElement;
        Assert.Equal(new[] { "author", "createdAt", "game", "gameId", "id", "text", "updatedAt", "userId" },
            root.EnumerateObject().Select(property => property.Name).Order());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("id").GetString()));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("updatedAt").ValueKind);
        var createdAt = root.GetProperty("createdAt").GetString();
        Assert.True(DateTimeOffset.TryParse(createdAt, out var instant));
        Assert.Equal(TimeSpan.Zero, instant.Offset);
        Assert.True(createdAt!.EndsWith("Z", StringComparison.Ordinal) || createdAt.EndsWith("+00:00", StringComparison.Ordinal));
        Assert.Equal(new[] { "avatarUrl", "displayName", "id" }, root.GetProperty("author").EnumerateObject().Select(property => property.Name).Order());
        using var response = await client.GetAsync("/api/v1/games/igdb-1");
        Assert.Equal(200, (int)response.StatusCode);
        using var game = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, game.RootElement.GetProperty("averageRating").ValueKind);
        Assert.Equal(JsonValueKind.Null, game.RootElement.GetProperty("coverUrl").ValueKind);
        Assert.True(DateOnly.TryParseExact(game.RootElement.GetProperty("releaseDate").GetString(), "yyyy-MM-dd", out _));
    }
}
