using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Tcc.Api.Tests.Identity;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class ProfilePersistenceTests
{
    [PostgresFact]
    public async Task Concurrent_partial_edits_preserve_fields_clear_avatar_and_survive_restart()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Profile", "profile@example.com", "synthetic-password"));
        registered.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, "profile@example.com", "synthetic-password");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        using var avatar = await Patch(client, "{\"avatarUrl\":\"https://example.com/avatar.png\"}");
        avatar.EnsureSuccessStatusCode();
        var edits = await Task.WhenAll(Patch(client, "{\"displayName\":\"Updated\"}"), Patch(client, "{\"bio\":\"Persisted bio\"}"));
        try { foreach (var response in edits) response.EnsureSuccessStatusCode(); }
        finally { foreach (var response in edits) response.Dispose(); }
        using var clear = await Patch(client, "{\"avatarUrl\":null}");
        clear.EnsureSuccessStatusCode();
        using var invalid = await Patch(client, "{\"displayName\":\" \",\"bio\":\"Must roll back\"}");
        Assert.Equal(400, (int)invalid.StatusCode);
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        var restored = await other.GetFromJsonAsync<PublicUser>("/api/v1/users/" + login.User.Id);
        Assert.Equal("Updated", restored?.DisplayName);
        Assert.Equal("Persisted bio", restored?.Bio);
        Assert.Null(restored?.AvatarUrl);
        await using var db = PostgresTestDatabase.Context();
        var account = await db.Users.SingleAsync();
        Assert.Equal("Updated", account.Profile.DisplayName);
        Assert.True(account.UpdatedAt >= account.CreatedAt);
    }

    [PostgresFact]
    public async Task Profile_insert_failure_rolls_back_the_account_and_returns_safe_storage_error()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_test_profile() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic-profile-failure' USING ERRCODE = '23514'; END $$;
            CREATE TRIGGER reject_test_profile BEFORE INSERT ON perfis_usuarios
            FOR EACH ROW EXECUTE FUNCTION reject_test_profile();
            """);
        try
        {
            await using var host = new PostgresApiFactory();
            using var client = host.CreateClient();
            using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Rollback", "rollback@example.com", "synthetic-password"));
            Assert.Equal(503, (int)response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ErrorResult>();
            Assert.Equal("storage_unavailable", error?.Code);
            Assert.Equal(0, await db.Users.CountAsync());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_test_profile ON perfis_usuarios; DROP FUNCTION reject_test_profile()");
        }
    }

    private static Task<HttpResponseMessage> Patch(HttpClient client, string json) => client.PatchAsync("/api/v1/users/me/profile", new StringContent(json, Encoding.UTF8, "application/json"));
}
