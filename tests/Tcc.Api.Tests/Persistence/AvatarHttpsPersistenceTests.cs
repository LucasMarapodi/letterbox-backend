using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tcc.Api.Tests.Identity;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class AvatarHttpsPersistenceTests
{
    [PostgresFact]
    public async Task Historical_http_avatar_round_trips_and_unrelated_edits_work_while_replacement_requires_https()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient(new() { BaseAddress = new("https://localhost") });
        var owner = await NotificationPersistenceTests.Register(client, "avatar-legacy@example.com");
        const string legacy = "http://example.com/legacy.png";
        await using (var db = PostgresTestDatabase.Context())
        {
            // Simulate existing data without changing the schema or involving external image servers.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE perfis_usuarios SET avatar_url={legacy} WHERE usuario_id={owner.Id}");
        }
        using (var patch = await client.PatchAsJsonAsync("/api/v1/users/me/profile", new { displayName = "Renamed", bio = "Updated" }))
            Assert.Equal(200, (int)patch.StatusCode);
        using (var completion = await client.PutAsJsonAsync("/api/v1/users/me/profile-completion", new { country = (string?)null, state = (string?)null, bio = "Completion", avatarUrl = legacy, favoriteGameIds = Array.Empty<int>() }))
            Assert.Equal(200, (int)completion.StatusCode);
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient(new() { BaseAddress = new("https://localhost") });
        var restored = await other.GetFromJsonAsync<PublicUser>($"/api/v1/users/{owner.Id}");
        Assert.Equal(legacy, restored?.AvatarUrl);
        Assert.Equal("Renamed", restored?.DisplayName);
        foreach (var path in new[] { "/api/v1/users/me/profile", "/api/v1/users/me/profile-completion" })
        {
            using var invalid = path.EndsWith("profile", StringComparison.Ordinal)
                ? await client.PatchAsJsonAsync(path, new { avatarUrl = "http://example.com/replacement.png" })
                : await client.PutAsJsonAsync(path, new { bio = "Must not persist", avatarUrl = "http://example.com/replacement.png", favoriteGameIds = Array.Empty<int>() });
            Assert.Equal(400, (int)invalid.StatusCode);
            Assert.Equal("validation_failed", (await invalid.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        }
        await using (var db = PostgresTestDatabase.Context())
        {
            var saved = await db.Users.SingleAsync();
            Assert.Equal(legacy, saved.Profile.AvatarUrl);
            Assert.Equal("Completion", saved.Profile.Bio);
        }
        using (var replace = await client.PatchAsJsonAsync("/api/v1/users/me/profile", new { avatarUrl = "https://example.com/new.png" }))
            Assert.Equal(200, (int)replace.StatusCode);
        using (var remove = await client.PutAsJsonAsync("/api/v1/users/me/profile-completion", new { avatarUrl = "", favoriteGameIds = Array.Empty<int>() }))
            Assert.Equal(200, (int)remove.StatusCode);
        Assert.Null((await other.GetFromJsonAsync<PublicUser>($"/api/v1/users/{owner.Id}"))?.AvatarUrl);
    }
}
