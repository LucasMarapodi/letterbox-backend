using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tcc.Api.Tests.Identity;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class SessionPersistenceTests
{
    [Fact]
    public void Session_model_has_integer_keys_hash_only_unique_index_and_restrict_owner()
    {
        using var context = new TccDbContext(new DbContextOptionsBuilder<TccDbContext>().UseNpgsql(BootstrapTests.SyntheticConnection).Options);
        var session = context.Model.FindEntityType("Tcc.Domain.Identity.UserSession");
        Assert.NotNull(session);
        Assert.Equal("sessoes_usuarios", session.GetTableName());
        Assert.Equal(typeof(int), session.FindProperty("Id")?.ClrType);
        Assert.Equal(typeof(int), session.FindProperty("UserId")?.ClrType);
        Assert.Null(session.FindProperty("Token"));
        Assert.NotNull(session.FindProperty("TokenHash"));
        Assert.Contains(session.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == "TokenHash");
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(session.GetForeignKeys()).DeleteBehavior);
    }

    [PostgresFact]
    public async Task Login_survives_host_restart_logout_revokes_only_current_session_and_no_plain_token_is_stored()
    {
        await PostgresApiFactory.ResetAsync();
        await using var first = new PostgresApiFactory();
        using var client = first.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Session", "session@example.com", "synthetic-password"));
        registered.EnsureSuccessStatusCode();
        var a = await LoginTests.Login(client, "session@example.com", "synthetic-password");
        var b = await LoginTests.Login(client, "session@example.com", "synthetic-password");
        Assert.True(a.Token != b.Token, "Cada login deve gerar token distinto.");
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", a.Token);
        using var me = await other.GetAsync("/api/v1/auth/me");
        Assert.Equal(200, (int)me.StatusCode);
        using var logout = await other.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(204, (int)logout.StatusCode);
        using var denied = await other.GetAsync("/api/v1/auth/me");
        Assert.Equal(401, (int)denied.StatusCode);
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", b.Token);
        using var valid = await other.GetAsync("/api/v1/auth/me");
        Assert.Equal(200, (int)valid.StatusCode);
        await using var context = PostgresTestDatabase.Context();
        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT token_hash, revogado_em IS NOT NULL FROM sessoes_usuarios ORDER BY id";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = 0;
        while (await reader.ReadAsync())
        {
            var hash = reader.GetString(0);
            Assert.True(hash != a.Token && hash != b.Token, "Banco deve guardar somente verificadores de token.");
            Assert.Equal(64, hash.Length);
            Assert.Equal(rows == 0, reader.GetBoolean(1));
            rows++;
        }
        Assert.Equal(2, rows);
    }
}
