using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class ProfileCompletionTests
{
    private const string Path = "/api/v1/users/me/profile-completion";

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Completion_requires_a_session_before_accessing_private_data(string method)
    {
        await using var host = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.SendAsync(new(new HttpMethod(method), Path));
        Assert.Equal(401, (int)response.StatusCode);
    }

    [PostgresFact]
    public async Task Completion_is_private_idempotent_durable_and_recalculated_from_current_profile_and_favorites()
    {
        await PostgresCatalogTests.Reset();
        var ids = await Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "completion-owner@example.com");
        using var initial = await Read(client);
        Assert.False(initial.RootElement.GetProperty("isComplete").GetBoolean());
        using var saved = await client.PutAsJsonAsync(Path, new { country = " Private country ", state = " State ", bio = " Description ", favoriteGameIds = ids.Take(3) });
        Assert.Equal(200, (int)saved.StatusCode);
        Assert.True(saved.Headers.CacheControl?.NoStore);
        using var first = JsonDocument.Parse(await saved.Content.ReadAsStringAsync());
        Assert.True(first.RootElement.GetProperty("isComplete").GetBoolean());
        Assert.Equal("Private country", first.RootElement.GetProperty("country").GetString());
        Assert.Equal(3, first.RootElement.GetProperty("favoriteGameCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, first.RootElement.GetProperty("avatarUrl").ValueKind);
        await using var db = PostgresTestDatabase.Context();
        var favorites = await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var timestamp = (await db.Users.AsNoTracking().SingleAsync()).Profile.UpdatedAt;
        using var repeat = await client.PutAsJsonAsync(Path, new { country = "Private country", state = "State", bio = "Description", favoriteGameIds = ids.Take(3) });
        repeat.EnsureSuccessStatusCode();
        Assert.Equal(first.RootElement.GetRawText(), await repeat.Content.ReadAsStringAsync());
        var restoredFavorites = await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        Assert.Equal(favorites.Select(item => (item.Id, item.CreatedAt)), restoredFavorites.Select(item => (item.Id, item.CreatedAt)));
        Assert.Equal(timestamp, (await db.Users.AsNoTracking().SingleAsync()).Profile.UpdatedAt);
        foreach (var route in new[] { $"/api/v1/users/{owner.Id}", "/api/v1/auth/me" })
        {
            var publicBody = await client.GetStringAsync(route);
            Assert.DoesNotContain("Private country", publicBody);
            using var json = JsonDocument.Parse(publicBody);
            Assert.False(json.RootElement.TryGetProperty("country", out _));
            Assert.False(json.RootElement.TryGetProperty("state", out _));
        }
        using (var avatar = await client.PatchAsJsonAsync("/api/v1/users/me/profile", new { avatarUrl = "https://example.com/avatar.png" })) avatar.EnsureSuccessStatusCode();
        using (var preserve = await client.PutAsJsonAsync(Path, new { country = "Private country", state = "State", bio = "Description", favoriteGameIds = ids })) preserve.EnsureSuccessStatusCode();
        using (var withAvatar = await Read(client))
        {
            Assert.Equal("https://example.com/avatar.png", withAvatar.RootElement.GetProperty("avatarUrl").GetString());
            Assert.Equal(4, withAvatar.RootElement.GetProperty("favoriteGameCount").GetInt32());
        }
        using (var remove = await client.PutAsJsonAsync(Path, new { country = "Private country", state = "State", bio = "Description", avatarUrl = (string?)null, favoriteGameIds = ids.Take(3) })) remove.EnsureSuccessStatusCode();
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        other.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);
        using (var restored = await Read(other)) Assert.Equal(first.RootElement.GetRawText(), restored.RootElement.GetRawText());
        using (var removeFavorite = await other.DeleteAsync($"/api/v1/favorites/game/{ids[2]}")) Assert.Equal(204, (int)removeFavorite.StatusCode);
        using (var incomplete = await Read(other)) Assert.False(incomplete.RootElement.GetProperty("isComplete").GetBoolean());
        using (var add = await other.PutAsync($"/api/v1/favorites/game/{ids[2]}", null)) add.EnsureSuccessStatusCode();
        using (var bio = await other.PatchAsJsonAsync("/api/v1/users/me/profile", new { bio = " " })) bio.EnsureSuccessStatusCode();
        using (var incomplete = await Read(other)) Assert.False(incomplete.RootElement.GetProperty("isComplete").GetBoolean());
        using (var bio = await other.PatchAsJsonAsync("/api/v1/users/me/profile", new { bio = "Restored", country = (string?)null })) bio.EnsureSuccessStatusCode();
        using (var incomplete = await Read(other))
        {
            Assert.False(incomplete.RootElement.GetProperty("isComplete").GetBoolean());
            Assert.Equal(JsonValueKind.Null, incomplete.RootElement.GetProperty("country").ValueKind);
        }
        await NotificationPersistenceTests.Register(other, "completion-other@example.com");
        using var unrelated = await Read(other);
        Assert.Equal(0, unrelated.RootElement.GetProperty("favoriteGameCount").GetInt32());
        Assert.False(unrelated.RootElement.GetProperty("isComplete").GetBoolean());
    }

    [PostgresFact]
    public async Task Invalid_selection_missing_game_and_unknown_user_field_never_partially_write()
    {
        await PostgresCatalogTests.Reset();
        var ids = await Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        await NotificationPersistenceTests.Register(client, "completion-validation@example.com");
        var bodies = new object[]
        {
            new { country = " ", state = "State", bio = "Bio", favoriteGameIds = ids },
            new { country = "Country", state = " ", bio = "Bio", favoriteGameIds = ids },
            new { country = "Country", state = "State", bio = " ", favoriteGameIds = ids },
            new { country = new string('x',101), state = "State", bio = "Bio", favoriteGameIds = ids },
            new { country = "Country", state = "State", bio = "Bio", favoriteGameIds = new[] { ids[0], ids[0], ids[0] } },
            new { country = "Country", state = "State", bio = "Bio", favoriteGameIds = new[] { ids[0], ids[1], 0 } },
            new { country = "Country", state = "State", bio = "Bio", favoriteGameIds = ids, avatarUrl = "file:///invalid" },
            new { country = "Country", state = "State", bio = "Bio", favoriteGameIds = ids, userId = 999 }
        };
        foreach (var body in bodies)
        {
            using var invalid = await client.PutAsJsonAsync(Path, body);
            Assert.Equal(400, (int)invalid.StatusCode);
        }
        using var missing = await client.PutAsJsonAsync(Path, new { country = "Country", state = "State", bio = "Bio", favoriteGameIds = new[] { ids[0], ids[1], int.MaxValue } });
        Assert.Equal(404, (int)missing.StatusCode);
        using var unchanged = await Read(client);
        Assert.Equal(0, unchanged.RootElement.GetProperty("favoriteGameCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, unchanged.RootElement.GetProperty("country").ValueKind);
    }

    internal static async Task<int[]> Games()
    {
        await using var db = PostgresTestDatabase.Context();
        return (await PostgresCatalogTests.Repository(db).UpsertAsync(Enumerable.Range(1,4).Select(index => PostgresCatalogTests.Payload(3_000_000_100L + index)).ToArray(), default))
            .Select(item => item.Game.Id).ToArray();
    }
    internal static async Task<JsonDocument> Read(HttpClient client)
    {
        using var result = await client.GetAsync(Path);
        Assert.Equal(200, (int)result.StatusCode);
        return JsonDocument.Parse(await result.Content.ReadAsStringAsync());
    }
}
