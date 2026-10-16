using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcc.Application.Catalog;
using Tcc.Domain.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class CatalogFilterIntegrityTests
{
    [PostgresFact]
    public async Task SQL_applies_all_filters_before_paging_and_snapshots_and_cache_keep_filter_identity()
    {
        await PostgresCatalogTests.Reset();
        await using var db = PostgresTestDatabase.Context();
        var now = DateTimeOffset.UtcNow;
        var rows = Enumerable.Range(1, 7).Select(index =>
        {
            var item = PostgresCatalogTests.Payload(3_000_000_100L + index);
            return item with { Game = item.Game with
            {
                Title = "Game " + index,
                ReleaseDate = index switch { 1 => new(2020, 1, 1), 4 => DateOnly.FromDateTime(now.AddYears(1).UtcDateTime), 5 => null, _ => new(2024, 1, 1) },
                GenreEntries = [new Genre(0, index == 6 ? 22 : 11, index == 6 ? "Other" : "RPG")],
                PlatformEntries = [new Platform(0, index == 7 ? 44 : 33, index == 7 ? "Console" : "PC")]
            } };
        }).ToArray();
        var repository = PostgresCatalogTests.Repository(db);
        await repository.UpsertAsync(rows, default);
        var genre = await db.Genres.SingleAsync(item => item.IgdbId == 11);
        var other = await db.Genres.SingleAsync(item => item.IgdbId == 22);
        var platform = await db.Platforms.SingleAsync(item => item.IgdbId == 33);
        var q1 = CatalogSearch.Create("Game", "release-desc", pageSize: 1, genreId: genre.Id, platformId: platform.Id);
        var q2 = CatalogSearch.Create("Game", "release-desc", page: 2, pageSize: 1, genreId: genre.Id, platformId: platform.Id);
        var first = await repository.ImportReleasePageAsync(q1, rows, now, default);
        var second = await repository.ImportReleasePageAsync(q2, rows, now, default);
        Assert.Equal("Game 2", Assert.Single(first.Items).Game.Title);
        Assert.Equal("Game 3", Assert.Single(second.Items).Game.Title);
        Assert.True(second.HasNextPage);
        var q3 = CatalogSearch.Create("Game", "release-desc", page: 3, pageSize: 1, genreId: genre.Id, platformId: platform.Id);
        var third = await repository.ImportReleasePageAsync(q3, rows, now, default);
        Assert.Equal("Game 1", Assert.Single(third.Items).Game.Title);
        Assert.False(third.HasNextPage);
        var platformOnly = await repository.ImportReleasePageAsync(CatalogSearch.Create("Game", "release-desc", platformId: platform.Id), rows, now, default);
        Assert.Equal(4, platformOnly.Items.Count);
        var genreOnly = await repository.ImportReleasePageAsync(CatalogSearch.Create("Game", "release-desc", genreId: genre.Id), rows, now, default);
        Assert.Equal(4, genreOnly.Items.Count);
        var different = await repository.ImportReleasePageAsync(CatalogSearch.Create("Game", "release-desc", pageSize: 1, genreId: other.Id), rows, now, default);
        await repository.SavePageAsync(first, default);
        await repository.SavePageAsync(different, default);
        Assert.Equal(first.Items[0].Game.Id, Assert.Single((await repository.FindPageAsync(q1, default))!.Items).Game.Id);
        var cache = new InMemoryCatalogCache(TimeProvider.System, new());
        await cache.SavePageAsync(first, default);
        await cache.SavePageAsync(different, default);
        Assert.Equal(first.Items[0].Game.Id, Assert.Single((await cache.FindPageAsync(q1, default))!.Items).Game.Id);
    }

    [PostgresFact]
    public async Task Incomplete_local_options_are_not_success_when_metadata_sync_fails_and_retry_can_recover()
    {
        await PostgresCatalogTests.Reset();
        await using var db = PostgresTestDatabase.Context();
        await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default);
        var supplier = new CatalogFilterTests.Transport { InvalidMetadata = true };
        await using var host = CatalogFilterTests.Host(supplier);
        using var client = host.CreateClient();
        using var failed = await client.GetAsync("/api/v1/games/filters");
        Assert.Equal(502, (int)failed.StatusCode);
        Assert.DoesNotContain("synthetic-invalid-metadata", await failed.Content.ReadAsStringAsync());
        Assert.Equal(1, await db.Genres.CountAsync());
        supplier.InvalidMetadata = false;
        using var recovered = JsonDocument.Parse(await client.GetStringAsync("/api/v1/games/filters"));
        Assert.Equal(3, recovered.RootElement.GetProperty("genres").GetArrayLength());
    }

    [PostgresFact]
    public async Task Metadata_platform_write_failure_rolls_back_genres_and_returns_safe_storage_error()
    {
        await PostgresCatalogTests.Reset();
        await using var db = PostgresTestDatabase.Context();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_filter_platform() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic-filter-write' USING ERRCODE='23514'; END $$;
            CREATE TRIGGER reject_filter_platform BEFORE INSERT ON plataformas FOR EACH ROW EXECUTE FUNCTION reject_filter_platform();
            """);
        try
        {
            await using var host = CatalogFilterTests.Host(new());
            using var client = host.CreateClient();
            using var response = await client.GetAsync("/api/v1/games/filters");
            Assert.Equal(503, (int)response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("storage_unavailable", json.RootElement.GetProperty("code").GetString());
            Assert.Equal(0, await db.Genres.CountAsync());
            Assert.Equal(0, await db.Platforms.CountAsync());
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_filter_platform ON plataformas; DROP FUNCTION reject_filter_platform()"); }
    }
}
