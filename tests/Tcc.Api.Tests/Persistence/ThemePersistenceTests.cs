using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tcc.Api.Tests.Igdb;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class ThemePersistenceTests
{
    [Fact]
    public void Model_has_normalized_local_ids_long_external_ids_and_restrict_foreign_keys()
    {
        using var db = PostgresTestDatabase.Context();
        var theme = Assert.Single(db.Model.GetEntityTypes(), type => type.ClrType.Name == "Theme");
        Assert.Equal("temas", theme.GetTableName());
        Assert.Equal(typeof(int), theme.FindProperty("Id")!.ClrType);
        Assert.Equal(typeof(long), theme.FindProperty("IgdbId")!.ClrType);
        Assert.Contains(theme.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == "IgdbId");
        var link = Assert.Single(db.Model.GetEntityTypes(), type => type.ClrType.Name == "GameTheme");
        Assert.Equal(new[] { "GameId", "ThemeId" }, link.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(2, link.GetForeignKeys().Count());
        Assert.All(link.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
        Assert.Contains(link.GetIndexes(), index => index.Properties.Single().Name == "ThemeId");
    }

    [PostgresFact]
    public async Task Upsert_is_idempotent_retains_unknown_metadata_replaces_known_links_and_survives_restart()
    {
        await PostgresCatalogTests.Reset();
        await using var db = PostgresTestDatabase.Context();
        var repo = PostgresCatalogTests.Repository(db);
        var now = DateTimeOffset.UtcNow;
        var old = Assert.Single(await repo.UpsertAsync([ThemeMappingTests.Map("null", fetched: now)], default));
        Assert.Empty(Themes(old));
        var enriched = ThemeMappingTests.Map("""[{"id":3000000040,"name":"Fantasy"},{"id":3000000040,"name":"Fantasy"}]""", fetched: now.AddSeconds(1));
        var batch = await repo.UpsertAsync([enriched, enriched], default);
        Assert.Equal(2, batch.Count); // Existing port returns one result per requested item.
        Assert.Single(batch.Select(item => item.Game.Id).Distinct());
        var saved = batch[0];
        await repo.UpsertAsync([enriched], default);
        Assert.Equal(old.Game.Id, saved.Game.Id);
        Assert.Equal("Fantasy", Assert.Single(Themes(saved)));
        Assert.Equal("RPG", Assert.Single(saved.Game.Genres));
        var count = await FinalMigrationAuditTests.Read(db, "SELECT count(*)::text FROM temas UNION ALL SELECT count(*)::text FROM jogos_temas");
        Assert.Equal(new[] { "1", "1" }, count);
        var theme = await db.Themes.AsNoTracking().SingleAsync();
        Assert.True(theme.Id > 0);
        Assert.Equal(3_000_000_040, theme.IgdbId);
        Assert.NotEqual(theme.IgdbId, theme.Id);
        await repo.UpsertAsync([ThemeMappingTests.Map("null", fetched: now.AddSeconds(2))], default);
        await using var restarted = PostgresTestDatabase.Context();
        var restored = (await PostgresCatalogTests.Repository(restarted).FindGamesAsync([old.Game.Id], default))[old.Game.Id];
        Assert.Equal("Fantasy", Assert.Single(Themes(restored)));
        Assert.Equal(saved.Game.ThemesFetchedAt, restored.Game.ThemesFetchedAt);
        await repo.UpsertAsync([ThemeMappingTests.Map("[]", fetched: now.AddSeconds(3))], default);
        Assert.Empty(Themes((await repo.FindGamesAsync([old.Game.Id], default))[old.Game.Id]));
        await repo.UpsertAsync([enriched], default); // Older response must not restore cleared relationships.
        Assert.Empty(await FinalMigrationAuditTests.Read(db, "SELECT tema_id::text FROM jogos_temas"));
    }

    [PostgresFact]
    public async Task SQL_enforces_unique_external_ids_pairs_and_foreign_keys_and_restrict_deletes()
    {
        await PostgresCatalogTests.Reset();
        await using var db = PostgresTestDatabase.Context();
        var saved = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([ThemeMappingTests.Map("""[{"id":3000000040,"name":"Fantasy"}]""")], default));
        foreach (var sql in new[] {
            "INSERT INTO jogos_temas(jogo_id,tema_id) SELECT jogo_id,tema_id FROM jogos_temas",
            "INSERT INTO temas(igdb_id,nome) VALUES (3000000040,'Duplicate')" })
            Assert.Equal(PostgresErrorCodes.UniqueViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql))).SqlState);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("INSERT INTO jogos_temas VALUES (999999,999999)"))).SqlState);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM temas"))).SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("INSERT INTO temas(igdb_id,nome) VALUES (-1,'Invalid')"))).SqlState);
        Assert.True(saved.Game.Id > 0);
    }

    [PostgresFact]
    public async Task Migration_preserves_existing_game_genre_and_identity_without_banners()
    {
        await using var db = PostgresTestDatabase.Context();
        await PostgresTestDatabase.VerifyExclusiveAsync(db);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261021230648_ForumTopicsAndReplies");
        try
        {
            await db.Database.ExecuteSqlRawAsync("TRUNCATE jogos RESTART IDENTITY CASCADE; INSERT INTO jogos(igdb_id,nome,descricao,dados_obtidos_em,criado_em,atualizado_em) VALUES(3000000099,'Before themes','Old','2026-01-01Z','2026-01-01Z','2026-01-01Z')");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO generos(igdb_id,nome) VALUES(3000000098,'Preserved genre') ON CONFLICT(igdb_id) DO NOTHING; INSERT INTO jogos_generos(jogo_id,genero_id) SELECT j.id,g.id FROM jogos j CROSS JOIN generos g WHERE j.igdb_id=3000000099 AND g.igdb_id=3000000098");
            var before = await FinalMigrationAuditTests.Read(db, "SELECT id::text || '|' || igdb_id::text || '|' || nome FROM jogos WHERE igdb_id=3000000099");
            var genres = await FinalMigrationAuditTests.Read(db, "SELECT jogo_id::text || '|' || genero_id::text FROM jogos_generos ORDER BY jogo_id,genero_id");
            await db.Database.MigrateAsync();
            Assert.Equal(before, await FinalMigrationAuditTests.Read(db, "SELECT id::text || '|' || igdb_id::text || '|' || nome FROM jogos WHERE igdb_id=3000000099"));
            Assert.Equal(genres, await FinalMigrationAuditTests.Read(db, "SELECT jogo_id::text || '|' || genero_id::text FROM jogos_generos ORDER BY jogo_id,genero_id"));
            Assert.Empty(await FinalMigrationAuditTests.Read(db, "SELECT nome FROM temas"));
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally { await db.Database.MigrateAsync(); }
    }

    [PostgresFact]
    public async Task Missing_themes_do_not_block_local_reads_and_explicit_refresh_enriches_or_falls_back()
    {
        await PostgresCatalogTests.Reset();
        await using var db = PostgresTestDatabase.Context();
        var repo = PostgresCatalogTests.Repository(db);
        var old = Assert.Single(await repo.UpsertAsync([PostgresCatalogTests.Payload()], default));
        var unavailable = new PostgresCatalogTests.NoSupplier();
        var pipeline = new AsyncGameCatalog(new InMemoryCatalogCache(TimeProvider.System, new()), repo, unavailable);
        Assert.Empty((await pipeline.GetAsync(old.Game.Id))!.Game.Themes);
        Assert.Equal(0, unavailable.Calls);
        var fallback = await pipeline.RefreshAsync(old.Game.Id);
        Assert.NotNull(fallback);
        Assert.Equal("RPG", Assert.Single(fallback.Game.Genres));
        Assert.Null(fallback.Game.ThemesFetchedAt);
        var supplier = new RefreshSupplier();
        pipeline = new AsyncGameCatalog(new InMemoryCatalogCache(TimeProvider.System, new()), repo, supplier);
        var refreshed = await pipeline.RefreshAsync(old.Game.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(old.Game.Id, refreshed.Game.Id);
        Assert.Equal("Fantasy", Assert.Single(refreshed.Game.Themes));
        Assert.NotNull(refreshed.Game.ThemesFetchedAt);
        Assert.Equal(1, supplier.Calls);
    }

    [PostgresFact]
    public async Task Public_list_details_cache_and_SQL_round_trip_themes_without_supplier_on_read()
    {
        await PostgresCatalogTests.Reset();
        var supplier = new ThemeSupplier();
        await using var host = new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IExternalGameCatalog>(supplier)));
        using var client = host.CreateClient();
        using var page = JsonDocument.Parse(await client.GetStringAsync("/api/v1/games?term=Theme&pageSize=20"));
        var game = Assert.Single(page.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Fantasy", Assert.Single(game.GetProperty("themes").EnumerateArray()).GetString());
        var id = game.GetProperty("id").GetInt32();
        await client.GetStringAsync("/api/v1/games?term=Theme&pageSize=20");
        Assert.Equal(1, supplier.Calls);
        var offline = new PostgresCatalogTests.NoSupplier();
        await using var restarted = new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IExternalGameCatalog>(offline)));
        using var other = restarted.CreateClient();
        using var details = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/games/{id}"));
        Assert.Equal("Fantasy", details.RootElement.GetProperty("themes")[0].GetString());
        Assert.Equal(0, offline.Calls);
    }

    private static string[] Themes(CatalogGame game) => game.Game.Themes.ToArray();

    private sealed class ThemeSupplier : IExternalGameCatalog
    {
        public int Calls { get; private set; }
        public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken ct)
        { Calls++; var item = ThemeMappingTests.Map("""[{"id":40,"name":"Fantasy"}]"""); return Task.FromResult(new CatalogPage(query, [item], false, item.Metadata)); }
        public Task<IReadOnlyDictionary<long, CatalogGame>> ResolveAsync(IReadOnlyList<long> ids, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class RefreshSupplier : IExternalGameCatalog
    {
        public int Calls { get; private set; }
        public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<long, CatalogGame>> ResolveAsync(IReadOnlyList<long> ids, CancellationToken ct)
        {
            Calls++;
            var id = Assert.Single(ids);
            return Task.FromResult<IReadOnlyDictionary<long, CatalogGame>>(new Dictionary<long, CatalogGame>
                { [id] = ThemeMappingTests.Map("""[{"id":40,"name":"Fantasy"}]""", id) });
        }
    }
}
