using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tcc.Domain.Identity;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[CollectionDefinition("PostgreSQL identity", DisableParallelization = true)]
public sealed class PostgresIdentityCollection;

[Collection("PostgreSQL identity")]
public sealed class PostgresModelTests
{
    [PostgresFact]
    public async Task Migration_materializes_integer_account_and_owned_profile_in_real_PostgreSQL()
    {
        await using var context = PostgresTestDatabase.Context();
        await PostgresTestDatabase.VerifyExclusiveAsync(context);
        await context.Database.MigrateAsync();
        var instant = DateTimeOffset.UtcNow;
        var email = "model-" + System.Security.Cryptography.RandomNumberGenerator.GetHexString(12).ToLowerInvariant() + "@example.com";
        var account = new UserAccount(0, "", Email.Create(email), "synthetic-verifier", UserProfile.Create("Model", "Bio", updatedAt: instant), instant);
        context.Users.Add(account);
        await context.SaveChangesAsync();
        Assert.True(account.Id > 0);
        Assert.Equal("user-" + account.Id, account.Username);
        context.ChangeTracker.Clear();
        var restored = await context.Users.SingleAsync(user => user.Id == account.Id);
        Assert.Equal("Model", restored.Profile.DisplayName);
        Assert.Equal("Bio", restored.Profile.Bio);
        Assert.Equal(account.CreatedAt.ToUnixTimeSeconds(), restored.CreatedAt.ToUnixTimeSeconds());
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!File.Exists(Path.Combine(Igdb.SecurityRegressionTests.WorkspaceRoot(), ".env.postgres-test")))
            Skip = "PostgreSQL real requer .env.postgres-test em alvo exclusivo, separado da suíte sem banco.";
    }
}

internal static class PostgresTestDatabase
{
    public static string Connection()
    {
        var lines = File.ReadLines(Path.Combine(Igdb.SecurityRegressionTests.WorkspaceRoot(), ".env.postgres-test"));
        var connection = lines.Single(line => line.StartsWith("ConnectionStrings__TccDatabase=", StringComparison.Ordinal)).Split('=', 2)[1];
        var builder = new NpgsqlConnectionStringBuilder(connection) { IncludeErrorDetail = false };
        if (builder.Database != "tcc_pr01_tests" || builder.Host is not ("127.0.0.1" or "localhost"))
            throw new InvalidOperationException("O alvo deve ser o PostgreSQL local exclusivo de testes.");
        return builder.ConnectionString;
    }
    public static TccDbContext Context() => new(new DbContextOptionsBuilder<TccDbContext>().UseNpgsql(Connection())
        .UseLoggerFactory(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).Options);
    public static async Task VerifyExclusiveAsync(TccDbContext context)
    {
        await using var connection = new NpgsqlConnection(Connection());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname=current_database()", connection);
        if (await command.ExecuteScalarAsync() as string != "TCC exclusive automated tests P-PR-01")
            throw new InvalidOperationException("Banco não identificado como exclusivo de testes.");
        Assert.True(Connection() == context.Database.GetConnectionString(), "Contexto deve usar o alvo exclusivo de testes.");
    }
}
