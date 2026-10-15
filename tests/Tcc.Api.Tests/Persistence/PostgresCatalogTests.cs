using Microsoft.EntityFrameworkCore;
using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Catalog;
using Tcc.Infrastructure.Igdb;
using Tcc.Infrastructure.Persistence;
using Tcc.Infrastructure.Persistence.Repositories;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class PostgresCatalogTests
{
    private static readonly CatalogMemoryOptions Options = new();
    internal static PostgresGameCatalog Repository(TccDbContext db) => new(new Lazy<TccDbContext>(() => db), TimeProvider.System, Options);
    internal static CatalogGame Payload(long externalId = 3_000_000_001, DateTimeOffset? fetched = null) => new(
        new Game(0, "Persistent game", "Description", null, ["RPG"], ["PC"], new DateOnly(2020, 1, 1))
        {
            IgdbId = externalId,
            GenreEntries = [new Genre(0, 3_000_000_002, "RPG")],
            PlatformEntries = [new Platform(0, 3_000_000_003, "PC")]
        }, new(fetched ?? DateTimeOffset.UtcNow));
    internal static async Task Reset()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE jogos_generos, jogos_plataformas, jogos, generos, plataformas RESTART IDENTITY");
    }

    [PostgresFact]
    public async Task Upsert_assigns_local_identity_and_restart_reads_SQL_without_supplier()
    {
        await Reset();
        await using var first = PostgresTestDatabase.Context();
        var item = Assert.Single(await Repository(first).UpsertAsync([Payload()], default));
        Assert.True(item.Game.Id > 0);
        Assert.NotEqual(item.Game.IgdbId, item.Game.Id);
        await using var restarted = PostgresTestDatabase.Context();
        var cache = new InMemoryCatalogCache(TimeProvider.System, Options);
        var supplier = new NoSupplier();
        var pipeline = new AsyncGameCatalog(cache, Repository(restarted), supplier);
        var restored = await pipeline.GetAsync(item.Game.Id);
        Assert.NotNull(restored);
        Assert.Equal(item.Game.IgdbId, restored.Game.IgdbId);
        Assert.Equal(new[] { "RPG" }, restored.Game.Genres);
        Assert.Equal(new[] { "PC" }, restored.Game.Platforms);
        Assert.Equal(item.Metadata.FetchedAt, restored.Metadata.FetchedAt);
        Assert.Null(await pipeline.GetAsync(999999));
        Assert.Equal(0, supplier.Calls);
        Assert.Single(await restarted.Genres.ToListAsync());
        Assert.Single(await restarted.Platforms.ToListAsync());
    }

    [PostgresFact]
    public async Task Concurrent_upserts_share_external_identity_and_pairs_and_replace_associations_without_erasing_other_games()
    {
        await Reset();
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var db = PostgresTestDatabase.Context();
            return Assert.Single(await Repository(db).UpsertAsync([Payload()], default)).Game.Id;
        }));
        Assert.Single(results.Distinct());
        await using var context = PostgresTestDatabase.Context();
        var repo = Repository(context);
        var now = DateTimeOffset.UtcNow.AddMinutes(1);
        var second = Assert.Single(await repo.UpsertAsync([Payload(3_000_000_010, now)], default));
        var updated = Payload(fetched: now.AddSeconds(1));
        updated = updated with { Game = updated.Game with { Title = "Renamed", Genres = [], GenreEntries = [], Platforms = [], PlatformEntries = [] } };
        var saved = Assert.Single(await repo.UpsertAsync([updated], default));
        Assert.Equal(results[0], saved.Game.Id);
        await repo.UpsertAsync([Payload(fetched: now.AddMinutes(-1))], default);
        var found = await repo.FindGamesAsync([saved.Game.Id, second.Game.Id], default);
        Assert.Equal("Renamed", found[saved.Game.Id].Game.Title);
        Assert.Empty(found[saved.Game.Id].Game.Genres);
        Assert.Single(found[second.Game.Id].Game.Genres);
        Assert.Equal(2, await context.Games.CountAsync());
        Assert.Equal(1, await context.Genres.CountAsync());
        Assert.Equal(1, await context.GameGenres.CountAsync());
        Assert.Equal(1, await context.GamePlatforms.CountAsync());
    }

    [PostgresFact]
    public async Task Association_failure_rolls_back_entire_catalog_batch()
    {
        await Reset();
        await using var db = PostgresTestDatabase.Context();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_test_catalog() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic-catalog-failure' USING ERRCODE = '23514'; END $$;
            CREATE TRIGGER reject_test_catalog BEFORE INSERT ON jogos_plataformas
            FOR EACH ROW EXECUTE FUNCTION reject_test_catalog();
            """);
        try
        {
            await Assert.ThrowsAsync<DatabaseUnavailableException>(() => Repository(db).UpsertAsync([Payload(), Payload(3_000_000_010)], default));
            Assert.Equal(0, await db.Games.CountAsync());
            Assert.Equal(0, await db.Genres.CountAsync());
            Assert.Equal(0, await db.Platforms.CountAsync());
            Assert.Equal(0, await db.GameGenres.CountAsync());
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_test_catalog ON jogos_plataformas; DROP FUNCTION reject_test_catalog()"); }
    }

    internal sealed class NoSupplier : IExternalGameCatalog
    {
        public int Calls { get; private set; }
        public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken ct) { Calls++; throw new CatalogIntegrationUnavailableException(); }
        public Task<IReadOnlyDictionary<long, CatalogGame>> ResolveAsync(IReadOnlyList<long> ids, CancellationToken ct) { Calls++; throw new CatalogIntegrationUnavailableException(); }
    }
}
