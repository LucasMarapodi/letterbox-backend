using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tcc.Api.DependencyInjection;
using Tcc.Api.Tests.Identity;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class PostgresIdentityHttpTests
{
    [PostgresFact]
    public async Task Password_policy_rejects_without_insert_and_preserves_real_hash_across_restart()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        using var invalid = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Policy", "policy-sql@example.com", "abc1!x"));
        Assert.Equal(400, (int)invalid.StatusCode);
        await using (var db = PostgresTestDatabase.Context()) Assert.Equal(0, await db.Users.CountAsync());
        const string password = " Abc1!x ";
        using var valid = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Policy", "policy-sql@example.com", password));
        Assert.Equal(201, (int)valid.StatusCode);
        var hasher = new Tcc.Infrastructure.Identity.Pbkdf2PasswordHasher();
        await using (var db = PostgresTestDatabase.Context())
        {
            var saved = await db.Users.SingleAsync();
            Assert.NotEqual(password, saved.PasswordHash);
            Assert.StartsWith("pbkdf2-sha512:210000:", saved.PasswordHash);
            Assert.True(hasher.Verify(password, saved.PasswordHash));
            Assert.False(hasher.Verify(password.Trim(), saved.PasswordHash));
            // Represents an account created before the new registration policy.
            db.Users.Add(new Tcc.Domain.Identity.UserAccount(0, "", Tcc.Domain.Identity.Email.Create("legacy-sql@example.com"),
                hasher.Hash("legacy"), Tcc.Domain.Identity.UserProfile.Create("Legacy")));
            await db.SaveChangesAsync();
        }
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        using var login = await other.PostAsJsonAsync("/api/v1/auth/login", new LoginInput("policy-sql@example.com", password));
        Assert.Equal(200, (int)login.StatusCode);
        using var legacy = await other.PostAsJsonAsync("/api/v1/auth/login", new LoginInput("legacy-sql@example.com", "legacy"));
        Assert.Equal(200, (int)legacy.StatusCode);
    }

    [PostgresFact]
    public async Task Register_is_atomic_returns_numeric_id_and_survives_a_new_host()
    {
        await PostgresApiFactory.ResetAsync();
        await using var first = new PostgresApiFactory();
        using var client = first.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Persisted", "persisted@example.com", "Synthetic1!password"));
        Assert.Equal(201, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Number, json.RootElement.GetProperty("id").ValueKind);
        var user = await response.Content.ReadFromJsonAsync<PublicUser>();
        Assert.NotNull(user);
        Assert.True(user.Id > 0);
        Assert.Equal("user-" + user.Id, user.Username);
        await using var context = PostgresTestDatabase.Context();
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal("Persisted", (await context.Users.SingleAsync()).Profile.DisplayName);
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        var restored = await other.GetFromJsonAsync<PublicUser>("/api/v1/users/" + user.Id);
        Assert.Equal(user, restored);
        Assert.DoesNotContain("password", json.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task Concurrent_normalized_email_registration_has_one_account_and_profile()
    {
        await PostgresApiFactory.ResetAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(index => client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterInput("Concurrent", index % 2 == 0 ? " ConCurrent@example.com " : "concurrent@example.com", "Synthetic1!password"))));
        try
        {
            Assert.Single(responses, response => (int)response.StatusCode == 201);
            Assert.Equal(11, responses.Count(response => (int)response.StatusCode == 409));
        }
        finally { foreach (var response in responses) response.Dispose(); }
        await using var context = PostgresTestDatabase.Context();
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal("concurrent@example.com", (await context.Users.SingleAsync()).Email.Value);
    }
}

public sealed class PostgresApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services => services.AddPostgresPersistence(_ => PostgresTestDatabase.Connection()));
    }
    internal static async Task ResetAsync()
    {
        await using var context = PostgresTestDatabase.Context();
        await PostgresTestDatabase.VerifyExclusiveAsync(context);
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE perfis_usuarios, usuarios RESTART IDENTITY CASCADE");
    }
}
