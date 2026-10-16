using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Application.Identity;
using Tcc.Domain.Identity;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class PartialProfileTests
{
    private const string Path = "/api/v1/users/me/profile";
    private const string Completion = "/api/v1/users/me/profile-completion";

    [PostgresFact]
    public async Task Partial_patch_saves_zero_two_three_and_more_than_five_favorites_and_preserves_omissions()
    {
        await PostgresCatalogTests.Reset();
        var games = await Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "partial-profile@example.com");
        using (var name = await client.PatchAsJsonAsync(Path, new { displayName = "Changed", avatarUrl = "https://example.com/avatar.png" }))
        {
            Assert.Equal(200, (int)name.StatusCode);
            Assert.True(name.Headers.CacheControl?.NoStore);
            using var json = JsonDocument.Parse(await name.Content.ReadAsStringAsync());
            Assert.Equal("Changed", json.RootElement.GetProperty("displayName").GetString());
            Assert.False(json.RootElement.GetProperty("completion").GetProperty("isComplete").GetBoolean());
        }
        foreach (var count in new[] { 0, 2, 3, 6 })
        {
            using var save = await client.PatchAsJsonAsync(Path, new { country = "Brasil", state = "São Paulo", bio = " Bio ", favoriteGameIds = games.Take(count).Concat(games.Take(count)) });
            Assert.Equal(200, (int)save.StatusCode);
            using var json = JsonDocument.Parse(await save.Content.ReadAsStringAsync());
            Assert.Equal("Changed", json.RootElement.GetProperty("displayName").GetString());
            Assert.Equal(count, json.RootElement.GetProperty("completion").GetProperty("favoriteGameCount").GetInt32());
            Assert.Equal(count >= 3, json.RootElement.GetProperty("completion").GetProperty("isComplete").GetBoolean());
        }
        await using var db = PostgresTestDatabase.Context();
        var favorites = await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        using (var omitted = await client.PatchAsJsonAsync(Path, new { displayName = "Only name" })) omitted.EnsureSuccessStatusCode();
        Assert.Equal(favorites, await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync());
        var timestamp = (await db.Users.AsNoTracking().SingleAsync()).Profile.UpdatedAt;
        using (var repeated = await client.PatchAsJsonAsync(Path, new { displayName = "Only name" })) repeated.EnsureSuccessStatusCode();
        Assert.Equal(timestamp, (await db.Users.AsNoTracking().SingleAsync()).Profile.UpdatedAt);
        using (var clear = await client.PatchAsJsonAsync(Path, new { bio = (string?)null, avatarUrl = (string?)null, country = (string?)null, state = (string?)null, favoriteGameIds = Array.Empty<int>() })) clear.EnsureSuccessStatusCode();
        using var status = JsonDocument.Parse(await client.GetStringAsync(Completion));
        Assert.Equal("", status.RootElement.GetProperty("bio").GetString());
        Assert.Equal(JsonValueKind.Null, status.RootElement.GetProperty("avatarUrl").ValueKind);
        Assert.Equal(0, status.RootElement.GetProperty("favoriteGameCount").GetInt32());
        foreach (var route in new[] { $"/api/v1/users/{owner.Id}", "/api/v1/auth/me" })
        {
            using var json = JsonDocument.Parse(await client.GetStringAsync(route));
            Assert.False(json.RootElement.TryGetProperty("completion", out _));
            Assert.False(json.RootElement.TryGetProperty("country", out _));
        }
    }

    [PostgresFact]
    public async Task Composed_location_and_games_are_validated_without_losing_unchanged_legacy_data()
    {
        await PostgresCatalogTests.Reset();
        var games = await Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "partial-location@example.com");
        await using var db = PostgresTestDatabase.Context();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE perfis_usuarios SET pais={" Legacy "},estado={" Region "} WHERE usuario_id={owner.Id}");
        using (var unchanged = await client.PatchAsJsonAsync(Path, new { displayName = "Legacy name", favoriteGameIds = games.Take(2) })) unchanged.EnsureSuccessStatusCode();
        var legacy = (await db.Users.AsNoTracking().SingleAsync()).Profile;
        Assert.Equal(" Legacy ", legacy.Country);
        Assert.Equal(" Region ", legacy.State);
        foreach (var body in new object[] { new { country = "China" }, new { state = "Hubei" }, new { country = "Brasil", state = "Hubei", displayName = "Invalid" }, new { favoriteGameIds = (int[]?)null }, new { favoriteGameIds = new[] { 0 } }, new { displayName = " " } })
        {
            using var invalid = await client.PatchAsJsonAsync(Path, body);
            Assert.Equal(400, (int)invalid.StatusCode);
        }
        using (var missing = await client.PatchAsJsonAsync(Path, new { displayName = "Missing", favoriteGameIds = new[] { int.MaxValue } })) Assert.Equal(404, (int)missing.StatusCode);
        Assert.Equal("Legacy name", (await db.Users.AsNoTracking().SingleAsync()).Profile.DisplayName);
        using (var valid = await client.PatchAsJsonAsync(Path, new { country = "China", state = "Hubei" })) valid.EnsureSuccessStatusCode();
        using (var incompatible = await client.PatchAsJsonAsync(Path, new { country = "Brasil" })) Assert.Equal(400, (int)incompatible.StatusCode);
        using (var incomplete = await client.PatchAsJsonAsync(Path, new { state = "" })) incomplete.EnsureSuccessStatusCode();
        using (var legacyRoute = await client.PutAsJsonAsync(Completion, new { country = "China", state = "Taiwan", bio = "Bio", favoriteGameIds = games.Take(3) })) legacyRoute.EnsureSuccessStatusCode();
        using (var legacyPartial = await client.PutAsJsonAsync(Completion, new { country = "Brasil", state = "", bio = "", favoriteGameIds = Array.Empty<int>() })) legacyPartial.EnsureSuccessStatusCode();
    }

    [PostgresFact]
    public async Task Failed_save_rolls_back_name_location_avatar_and_favorites_and_partial_commands_merge_under_lock()
    {
        await PostgresCatalogTests.Reset();
        var games = await Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "partial-rollback@example.com");
        using (var baseline = await client.PatchAsJsonAsync(Path, new { displayName = "Before", country = "Brasil", state = "São Paulo", bio = "Bio", favoriteGameIds = games })) baseline.EnsureSuccessStatusCode();
        var before = await client.GetStringAsync(Completion);
        await using var db = PostgresTestDatabase.Context();
        var favorites = await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var interceptor = new FailProfileSave();
        await using var failing = host.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddDbContext<TccDbContext>(options => options.AddInterceptors(interceptor))));
        using var other = failing.CreateClient();
        other.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);
        using var failed = await other.PatchAsJsonAsync(Path, new { displayName = "Fail save", country = "China", state = "Hubei", avatarUrl = "https://example.com/new.png", favoriteGameIds = games.Take(2) });
        Assert.True(interceptor.Triggered);
        Assert.Equal(503, (int)failed.StatusCode);
        Assert.Equal(before, await client.GetStringAsync(Completion));
        Assert.Equal("Before", (await db.Users.AsNoTracking().SingleAsync()).Profile.DisplayName);
        Assert.Equal(favorites, await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync());
        var edits = await Task.WhenAll(client.PatchAsJsonAsync(Path, new { country = "China", state = "Hubei", favoriteGameIds = games.Take(2) }),
            client.PatchAsJsonAsync(Path, new { displayName = "Merged" }));
        try { Assert.All(edits, response => Assert.Equal(200, (int)response.StatusCode)); }
        finally { foreach (var response in edits) response.Dispose(); }
        Assert.Equal("Merged", (await db.Users.AsNoTracking().SingleAsync()).Profile.DisplayName);
        Assert.Equal("China", (await db.Users.AsNoTracking().SingleAsync()).Profile.Country);
        Assert.Equal(2, await db.GameFavorites.CountAsync());
    }

    private sealed class FailProfileSave : SaveChangesInterceptor
    {
        public bool Triggered { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<UserProfile>().Any(item => item.Entity.DisplayName == "Fail save") == true)
            { Triggered = true; throw new DatabaseUnavailableException(); }
            return ValueTask.FromResult(result);
        }
    }
    private static async Task<int[]> Games()
    {
        await using var db = PostgresTestDatabase.Context();
        return (await PostgresCatalogTests.Repository(db).UpsertAsync(Enumerable.Range(1,6).Select(index => PostgresCatalogTests.Payload(3_000_032_000L + index)).ToArray(), default)).Select(item => item.Game.Id).ToArray();
    }
}
