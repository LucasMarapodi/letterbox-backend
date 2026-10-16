using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Application.Identity;
using Tcc.Infrastructure.Persistence;

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
        using var saved = await client.PutAsJsonAsync(Path, new { country = " Brasil ", state = " São Paulo ", bio = " Description ", favoriteGameIds = ids.Take(3) });
        Assert.Equal(200, (int)saved.StatusCode);
        Assert.True(saved.Headers.CacheControl?.NoStore);
        using var first = JsonDocument.Parse(await saved.Content.ReadAsStringAsync());
        Assert.True(first.RootElement.GetProperty("isComplete").GetBoolean());
        Assert.Equal("Brasil", first.RootElement.GetProperty("country").GetString());
        Assert.Equal(3, first.RootElement.GetProperty("favoriteGameCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, first.RootElement.GetProperty("avatarUrl").ValueKind);
        await using var db = PostgresTestDatabase.Context();
        var favorites = await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var timestamp = (await db.Users.AsNoTracking().SingleAsync()).Profile.UpdatedAt;
        using var repeat = await client.PutAsJsonAsync(Path, new { country = "Brasil", state = "São Paulo", bio = "Description", favoriteGameIds = ids.Take(3) });
        repeat.EnsureSuccessStatusCode();
        Assert.Equal(first.RootElement.GetRawText(), await repeat.Content.ReadAsStringAsync());
        var restoredFavorites = await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        Assert.Equal(favorites.Select(item => (item.Id, item.CreatedAt)), restoredFavorites.Select(item => (item.Id, item.CreatedAt)));
        Assert.Equal(timestamp, (await db.Users.AsNoTracking().SingleAsync()).Profile.UpdatedAt);
        foreach (var route in new[] { $"/api/v1/users/{owner.Id}", "/api/v1/auth/me" })
        {
            var publicBody = await client.GetStringAsync(route);
            Assert.DoesNotContain("Brasil", publicBody);
            using var json = JsonDocument.Parse(publicBody);
            Assert.False(json.RootElement.TryGetProperty("country", out _));
            Assert.False(json.RootElement.TryGetProperty("state", out _));
        }
        using (var avatar = await client.PatchAsJsonAsync("/api/v1/users/me/profile", new { avatarUrl = "https://example.com/avatar.png" })) avatar.EnsureSuccessStatusCode();
        using (var preserve = await client.PutAsJsonAsync(Path, new { country = "Brasil", state = "São Paulo", bio = "Description", favoriteGameIds = ids })) preserve.EnsureSuccessStatusCode();
        using (var withAvatar = await Read(client))
        {
            Assert.Equal("https://example.com/avatar.png", withAvatar.RootElement.GetProperty("avatarUrl").GetString());
            Assert.Equal(4, withAvatar.RootElement.GetProperty("favoriteGameCount").GetInt32());
        }
        using (var remove = await client.PutAsJsonAsync(Path, new { country = "Brasil", state = "São Paulo", bio = "Description", avatarUrl = (string?)null, favoriteGameIds = ids.Take(3) })) remove.EnsureSuccessStatusCode();
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        other.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);
        using (var restored = await Read(other)) Assert.Equal(first.RootElement.GetRawText(), restored.RootElement.GetRawText());
        using (var removeFavorite = await other.DeleteAsync($"/api/v1/favorites/game/{ids[2]}")) Assert.Equal(204, (int)removeFavorite.StatusCode);
        using (var incomplete = await Read(other)) Assert.False(incomplete.RootElement.GetProperty("isComplete").GetBoolean());
        using (var add = await other.PutAsync($"/api/v1/favorites/game/{ids[2]}", null)) add.EnsureSuccessStatusCode();
        using (var bio = await other.PatchAsJsonAsync("/api/v1/users/me/profile", new { bio = " " })) bio.EnsureSuccessStatusCode();
        using (var incomplete = await Read(other)) Assert.False(incomplete.RootElement.GetProperty("isComplete").GetBoolean());
        using (var bio = await other.PatchAsJsonAsync("/api/v1/users/me/profile", new { bio = "Restored", country = (string?)null, state = (string?)null })) bio.EnsureSuccessStatusCode();
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
            new { country = " ", state = "São Paulo", bio = "Bio", favoriteGameIds = ids },
            new { country = "Brasil", state = "Hubei", bio = "Bio", favoriteGameIds = ids },
            new { country = "Brasil", state = "São Paulo", bio = new string('x',2001), favoriteGameIds = ids },
            new { country = new string('x',101), state = "São Paulo", bio = "Bio", favoriteGameIds = ids },
            new { country = "Brasil", state = "São Paulo", bio = "Bio", favoriteGameIds = new[] { ids[0], ids[0], -1 } },
            new { country = "Brasil", state = "São Paulo", bio = "Bio", favoriteGameIds = new[] { ids[0], ids[1], 0 } },
            new { country = "Brasil", state = "São Paulo", bio = "Bio", favoriteGameIds = ids, avatarUrl = "file:///invalid" },
            new { country = "Brasil", state = "São Paulo", bio = "Bio", favoriteGameIds = ids, userId = 999 }
        };
        foreach (var body in bodies)
        {
            using var invalid = await client.PutAsJsonAsync(Path, body);
            Assert.Equal(400, (int)invalid.StatusCode);
        }
        using var missing = await client.PutAsJsonAsync(Path, new { country = "Brasil", state = "São Paulo", bio = "Bio", favoriteGameIds = new[] { ids[0], ids[1], int.MaxValue } });
        Assert.Equal(404, (int)missing.StatusCode);
        using var unchanged = await Read(client);
        Assert.Equal(0, unchanged.RootElement.GetProperty("favoriteGameCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, unchanged.RootElement.GetProperty("country").ValueKind);
    }

    [PostgresFact]
    public async Task Failure_after_sql_save_rolls_back_profile_and_favorite_reconciliation()
    {
        await PostgresCatalogTests.Reset();
        var ids = await Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "completion-rollback@example.com");
        using (var save = await client.PutAsJsonAsync(Path, new { country = "Brasil", state = "São Paulo", bio = "Bio", favoriteGameIds = ids.Take(3) })) save.EnsureSuccessStatusCode();
        using var before = await Read(client);
        await using var db = PostgresTestDatabase.Context();
        var favorites = await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var profileTime = (await db.Users.AsNoTracking().SingleAsync()).Profile.UpdatedAt;
        var failure = new FailAfterCompletionSave();
        await using var failing = host.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<TccDbContext>(options => options.AddInterceptors(failure))));
        using var other = failing.CreateClient();
        other.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);
        using var result = await other.PutAsJsonAsync(Path, new { country = "China", state = "Hubei", bio = "Changed", favoriteGameIds = ids.Skip(1) });
        Assert.True(failure.Triggered);
        Assert.Equal(503, (int)result.StatusCode);
        Assert.Contains("storage_unavailable", await result.Content.ReadAsStringAsync());
        using var after = await Read(client);
        Assert.Equal(before.RootElement.GetRawText(), after.RootElement.GetRawText());
        Assert.Equal(favorites.Select(item => (item.Id, item.GameId, item.CreatedAt)),
            (await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync()).Select(item => (item.Id, item.GameId, item.CreatedAt)));
        Assert.Equal(profileTime, (await db.Users.AsNoTracking().SingleAsync()).Profile.UpdatedAt);
    }

    [PostgresFact]
    public async Task Concurrent_completion_commands_preserve_one_whole_selection_and_leave_review_favorites_untouched()
    {
        await PostgresCatalogTests.Reset();
        var ids = await Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        await NotificationPersistenceTests.Register(client, "completion-concurrent@example.com");
        using (var publish = await client.PostAsJsonAsync($"/api/v1/games/{ids[0]}/reviews", new { title = "Fixture review", rating = 3, text = "Review" }))
        {
            publish.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await publish.Content.ReadAsStringAsync());
            using var favorite = await client.PutAsync($"/api/v1/favorites/review/{json.RootElement.GetProperty("id").GetInt32()}", null);
            favorite.EnsureSuccessStatusCode();
        }
        await using var db = PostgresTestDatabase.Context();
        var reviews = await db.ReviewFavorites.AsNoTracking().ToArrayAsync();
        // Two games plus a review favorite still do not meet the requirement.
        foreach (var id in ids.Take(2))
        {
            using var favorite = await client.PutAsync($"/api/v1/favorites/game/{id}", null);
            favorite.EnsureSuccessStatusCode();
        }
        using (var current = await Read(client)) Assert.Equal(2, current.RootElement.GetProperty("favoriteGameCount").GetInt32());
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => client.PutAsJsonAsync(Path,
            new { country = index % 2 == 0 ? "Brasil" : "China", state = index % 2 == 0 ? "São Paulo" : "Hubei", bio = "Bio", favoriteGameIds = index % 2 == 0 ? ids.Take(3) : ids.Skip(1) })));
        try { Assert.All(results, result => Assert.Equal(200, (int)result.StatusCode)); }
        finally { foreach (var result in results) result.Dispose(); }
        using var saved = await Read(client);
        var root = saved.RootElement;
        Assert.True(root.GetProperty("isComplete").GetBoolean());
        Assert.Equal(root.GetProperty("country").GetString() == "Brasil" ? ids.Take(3) : ids.Skip(1),
            root.GetProperty("favoriteGameIds").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(reviews, await db.ReviewFavorites.AsNoTracking().ToArrayAsync());
        // The ordinary editor's avatar survives completion PUTs that omit it in either order.
        var edits = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => index % 2 == 0
            ? client.PatchAsJsonAsync("/api/v1/users/me/profile", new { avatarUrl = "https://example.com/concurrent.png" })
            : client.PutAsJsonAsync(Path, new { country = "Brasil", state = "São Paulo", bio = "Bio", favoriteGameIds = ids.Take(3).Concat(ids.Take(3)) })));
        try { Assert.All(edits, result => Assert.Equal(200, (int)result.StatusCode)); }
        finally { foreach (var result in edits) result.Dispose(); }
        using var combined = await Read(client);
        Assert.Equal("https://example.com/concurrent.png", combined.RootElement.GetProperty("avatarUrl").GetString());
        Assert.Equal("Brasil", combined.RootElement.GetProperty("country").GetString());
        Assert.Equal(3, combined.RootElement.GetProperty("favoriteGameCount").GetInt32());
    }

    private sealed class FailAfterCompletionSave : SaveChangesInterceptor
    {
        public bool Triggered { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<Tcc.Domain.Identity.UserProfile>().Any(entry => entry.Entity.Country == "China") == true)
            {
                Triggered = true;
                throw new DatabaseUnavailableException();
            }
            return ValueTask.FromResult(result);
        }
    }

    [PostgresFact]
    public async Task Location_migration_preserves_existing_account_profile_and_favorites()
    {
        await PostgresCatalogTests.Reset();
        var ids = await Games();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var owner = await NotificationPersistenceTests.Register(client, "completion-upgrade@example.com");
        using (var patch = await client.PatchAsJsonAsync("/api/v1/users/me/profile", new { bio = "Existing bio", avatarUrl = "https://example.com/photo.png" })) patch.EnsureSuccessStatusCode();
        foreach (var id in ids.Take(3))
        {
            using var favorite = await client.PutAsync($"/api/v1/favorites/game/{id}", null);
            favorite.EnsureSuccessStatusCode();
        }
        await using var db = PostgresTestDatabase.Context();
        await PostgresTestDatabase.VerifyExclusiveAsync(db);
        var account = await db.Users.AsNoTracking().SingleAsync();
        var favorites = await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(db.Database.GetMigrations().Single(name => name.EndsWith("PersistentNotifications", StringComparison.Ordinal)));
        try
        {
            await db.Database.MigrateAsync();
            var restored = await db.Users.AsNoTracking().SingleAsync();
            Assert.Equal(account.Id, restored.Id);
            Assert.Equal(account.Username, restored.Username);
            Assert.True(account.Email == restored.Email && account.PasswordHash == restored.PasswordHash);
            Assert.Equal(account.CreatedAt, restored.CreatedAt);
            Assert.Equal(account.UpdatedAt, restored.UpdatedAt);
            Assert.Equal(account.Profile, restored.Profile);
            Assert.Equal(favorites, await db.GameFavorites.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync());
            client.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);
            using var completion = await Read(client);
            Assert.False(completion.RootElement.GetProperty("isComplete").GetBoolean());
            Assert.Equal(3, completion.RootElement.GetProperty("favoriteGameCount").GetInt32());
            Assert.Equal(JsonValueKind.Null, completion.RootElement.GetProperty("country").ValueKind);
            Assert.Equal(JsonValueKind.Null, completion.RootElement.GetProperty("state").ValueKind);
        }
        finally { await db.Database.MigrateAsync(); }
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
