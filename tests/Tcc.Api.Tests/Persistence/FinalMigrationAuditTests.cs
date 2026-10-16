using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class FinalMigrationAuditTests
{
    [PostgresFact]
    public async Task Empty_schema_and_versioned_identity_baseline_upgrade_to_the_same_complete_schema()
    {
        await using var db = PostgresTestDatabase.Context();
        // Down is permitted only after both the database name and its exclusive-test marker agree.
        await PostgresTestDatabase.VerifyExclusiveAsync(db);
        var migrator = db.GetService<IMigrator>();
        var migrations = db.Database.GetMigrations().ToArray();
        Assert.Equal(7, migrations.Length);
        await migrator.MigrateAsync("0");
        try
        {
            Assert.Empty(await Read(db, "SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename <> '__EFMigrationsHistory'"));
            await db.Database.MigrateAsync();
            await AssertSchema(db);
            await migrator.MigrateAsync(migrations.Single(name => name.EndsWith("IdentitySessions", StringComparison.Ordinal)));
            await db.Database.ExecuteSqlRawAsync(IdentityUpgradeBaseline.Sql);
            var baseline = await Read(db, IdentityUpgradeBaseline.ReadSql);
            Assert.Equal(3, baseline.Count);
            await db.Database.MigrateAsync();
            Assert.Equal(baseline, await Read(db, IdentityUpgradeBaseline.ReadSql));
            await AssertSchema(db);
            Assert.Equal(migrations, await db.Database.GetAppliedMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            // Leave the shared, serialized test target usable even if an intermediate assertion fails.
            await db.Database.MigrateAsync();
        }
    }

    private static async Task AssertSchema(Tcc.Infrastructure.Persistence.TccDbContext db)
    {
        var tables = db.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables.ToArray();
        Assert.Equal(16, tables.Length);
        Assert.Equal(tables.Select(table => table.Name).Order(),
            await Read(db, "SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename <> '__EFMigrationsHistory' ORDER BY tablename"));
        foreach (var table in tables)
        {
            var columns = await Read(db, """
                SELECT a.attname || '|' || format_type(a.atttypid,a.atttypmod) || '|' ||
                       CASE WHEN a.attnotnull THEN 'required' ELSE 'nullable' END
                FROM pg_attribute a JOIN pg_class t ON t.oid=a.attrelid
                JOIN pg_namespace n ON n.oid=t.relnamespace
                WHERE n.nspname='public' AND t.relname=@table AND a.attnum>0 AND NOT a.attisdropped
                ORDER BY a.attname
                """, table.Name);
            Assert.Equal(table.Columns.Select(column => $"{column.Name}|{column.StoreType}|{(column.IsNullable ? "nullable" : "required")}").Order(), columns);
            var expectedConstraints = table.UniqueConstraints.Select(key => key.Name)
                .Concat(table.ForeignKeyConstraints.Select(key => key.Name)).Concat(table.CheckConstraints.Select(check => check.Name)).Order(StringComparer.Ordinal);
            Assert.Equal(expectedConstraints, (await Read(db, """
                SELECT c.conname FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid
                JOIN pg_namespace n ON n.oid=t.relnamespace
                WHERE n.nspname='public' AND t.relname=@table AND c.contype IN ('p','u','f','c') ORDER BY c.conname
                """, table.Name)).Order(StringComparer.Ordinal));
            foreach (var key in table.ForeignKeyConstraints)
            {
                Assert.All(key.Columns, column => Assert.Equal("integer", column.StoreType));
                var expected = $"{key.Name}|{string.Join(',', key.Columns.Select(column => column.Name))}|{key.PrincipalTable.Name}|{string.Join(',', key.PrincipalColumns.Select(column => column.Name))}|r";
                Assert.Contains(expected, await Read(db, """
                    SELECT c.conname || '|' || array_to_string(ARRAY(SELECT a.attname FROM unnest(c.conkey) WITH ORDINALITY k(id,pos) JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.id ORDER BY pos), ',')
                     || '|' || p.relname || '|' || array_to_string(ARRAY(SELECT a.attname FROM unnest(c.confkey) WITH ORDINALITY k(id,pos) JOIN pg_attribute a ON a.attrelid=c.confrelid AND a.attnum=k.id ORDER BY pos), ',') || '|' || c.confdeltype::text
                    FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_class p ON p.oid=c.confrelid
                    JOIN pg_namespace n ON n.oid=t.relnamespace WHERE n.nspname='public' AND t.relname=@table AND c.contype='f'
                    """, table.Name));
            }
            foreach (var index in table.Indexes)
            {
                Assert.Contains($"{index.Name}|{index.IsUnique}|{string.Join(',', index.Columns.Select(column => column.Name))}|{index.Filter is not null}", await Read(db, """
                    SELECT ci.relname || '|' || CASE WHEN i.indisunique THEN 'True' ELSE 'False' END || '|' ||
                      array_to_string(ARRAY(SELECT a.attname FROM unnest(i.indkey) WITH ORDINALITY k(id,pos) JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.id ORDER BY pos), ',') || '|' || CASE WHEN i.indpred IS NOT NULL THEN 'True' ELSE 'False' END
                    FROM pg_index i JOIN pg_class t ON t.oid=i.indrelid JOIN pg_class ci ON ci.oid=i.indexrelid
                    JOIN pg_namespace n ON n.oid=t.relnamespace WHERE n.nspname='public' AND t.relname=@table
                    """, table.Name));
            }
            Assert.All(table.PrimaryKey!.Columns, column => Assert.Equal("integer", column.StoreType));
            foreach (var column in table.Columns.Where(column => column.Name == "igdb_id")) Assert.Equal("bigint", column.StoreType);
            var identities = await Read(db, """
                SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name=@table AND is_identity='YES' ORDER BY column_name
                """, table.Name);
            Assert.Equal(table.PrimaryKey.Columns.Count == 1 && table.Name != "perfis_usuarios" ? new[] { "id" } : [], identities);
        }
    }

    internal static async Task<List<string>> Read(Tcc.Infrastructure.Persistence.TccDbContext db, string sql, string? table = null)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)db.Database.GetDbConnection());
        if (table is not null) command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add(reader.GetString(0));
        return rows;
    }
}

internal static class IdentityUpgradeBaseline
{
    // Synthetic fixture deliberately targets the historical two-migration schema.
    public const string Sql = """
        INSERT INTO usuarios(email,senha_hash,criado_em,atualizado_em)
        VALUES ('upgrade@example.com','synthetic-verifier','2026-01-01Z','2026-01-01Z');
        INSERT INTO perfis_usuarios(usuario_id,nome_exibicao,biografia,atualizado_em)
        SELECT id,'Upgrade baseline','Preserve this profile','2026-01-01Z' FROM usuarios WHERE email='upgrade@example.com';
        INSERT INTO sessoes_usuarios(usuario_id,token_hash,criado_em,expira_em)
        SELECT id,repeat('a',64),'2026-01-01Z','2030-01-01Z' FROM usuarios WHERE email='upgrade@example.com';
        """;
    // Compare IDs, relationships, public content and timestamps without printing verifiers.
    public const string ReadSql = """
        SELECT 'user|' || id || '|' || username || '|' || email || '|' || criado_em || '|' || atualizado_em FROM usuarios
        UNION ALL SELECT 'profile|' || usuario_id || '|' || nome_exibicao || '|' || biografia || '|' || atualizado_em FROM perfis_usuarios
        UNION ALL SELECT 'session|' || id || '|' || usuario_id || '|' || criado_em || '|' || expira_em FROM sessoes_usuarios ORDER BY 1
        """;
}
