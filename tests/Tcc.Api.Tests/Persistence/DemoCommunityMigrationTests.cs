using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Tcc.Domain.Identity;
using Tcc.Infrastructure.Identity;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class DemoCommunityMigrationTests
{
    [PostgresFact]
    public async Task Seed_creates_demo_content_with_valid_passwords_and_preserves_existing_accounts()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var migrations = db.GetService<IMigrationsAssembly>();
        var entry = Assert.Single(migrations.Migrations, item => item.Key.EndsWith("_DemoCommunity"));
        var migration = migrations.CreateMigration(entry.Value, db.Database.ProviderName!);
        var sql = Assert.IsType<SqlOperation>(Assert.Single(migration.UpOperations)).Sql;
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync(sql);
        const string password = "TccDemo@2026!";
        PasswordPolicy.Validate(password);
        var users = await db.Users.ToArrayAsync();
        Assert.Equal(6, users.Length);
        var hasher = new Pbkdf2PasswordHasher();
        Assert.All(users, user => Assert.True(hasher.Verify(password, user.PasswordHash)));
        Assert.Equal(6, users.Select(user => user.PasswordHash).Distinct().Count());
        Assert.Equal(6, await db.Reviews.CountAsync());
        Assert.Equal(6, await db.Ratings.CountAsync());
        Assert.Equal(6, await db.Comments.CountAsync());
        // A second execution must preserve accounts, passwords and content.
        await db.Database.ExecuteSqlRawAsync(sql);
        db.ChangeTracker.Clear();
        Assert.Equal(users.Select(user => user.PasswordHash).Order(),
            (await db.Users.ToArrayAsync()).Select(user => user.PasswordHash).Order());
        Assert.Equal(6, await db.Reviews.CountAsync());
        Assert.Equal(6, await db.Comments.CountAsync());
        var topics = await FinalMigrationAuditTests.Read(db, "SELECT count(*)::text FROM topicos_forum");
        Assert.Equal("2", Assert.Single(topics));
        var replies = await FinalMigrationAuditTests.Read(db, "SELECT count(*)::text FROM respostas_forum");
        Assert.Equal("6", Assert.Single(replies));
        await transaction.RollbackAsync();
    }
}
