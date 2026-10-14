using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class LocalCatalogTests
{
    private static TestClock Clock() => new() { Now = CatalogFlow.Metadata.FetchedAt };

    [Fact]
    public async Task Upsert_keeps_one_game_preserves_timestamp_and_does_not_overwrite_newer_data()
    {
        var clock = Clock();
        var local = new InMemoryLocalGameCatalog(clock, new());
        var old = CatalogFlow.Game("igdb-1");
        await local.SaveGamesAsync([old, old], default);
        var updated = old with { Game = old.Game with { Title = "Updated" }, Metadata = new(clock.Now.AddMinutes(1)) };
        await local.SaveGamesAsync([updated], default);
        await local.SaveGamesAsync([old], default);
        var result = Assert.Single((await local.FindGamesAsync(["igdb-1"], default)).Values);
        Assert.Equal("Updated", result.Game.Title);
        Assert.Equal(updated.Metadata.FetchedAt, result.Metadata.FetchedAt);
    }

    [Fact]
    public async Task Snapshot_eviction_does_not_remove_games_referenced_by_local_content()
    {
        var local = new InMemoryLocalGameCatalog(Clock(), new() { SnapshotLimit = 1 });
        var first = new CatalogPage(CatalogSearch.Create("first"), [CatalogFlow.Game("igdb-1")], true, CatalogFlow.Metadata);
        var second = new CatalogPage(CatalogSearch.Create("second"), [CatalogFlow.Game("igdb-2")], false, CatalogFlow.Metadata);
        await local.SavePageAsync(first, default);
        await local.SavePageAsync(second, default);
        Assert.Null(await local.FindPageAsync(first.Query, default));
        Assert.NotNull(await local.FindPageAsync(second.Query, default));
        Assert.Equal(2, (await local.FindGamesAsync(["igdb-1", "igdb-2"], default)).Count);
    }

    [Fact]
    public async Task Snapshot_is_exact_complete_ordered_and_does_not_filter_a_partial_collection()
    {
        var local = new InMemoryLocalGameCatalog(Clock(), new());
        var query = CatalogSearch.Create("Name", page: 2, pageSize: 2);
        var page = new CatalogPage(query, [CatalogFlow.Game("igdb-2"), CatalogFlow.Game("igdb-1")], true, CatalogFlow.Metadata);
        await local.SavePageAsync(page, default);
        var result = await local.FindPageAsync(CatalogSearch.Create("name", page: 2, pageSize: 2), default);
        Assert.NotNull(result);
        Assert.Equal(new[] { "igdb-2", "igdb-1" }, result.Items.Select(item => item.Game.Id));
        Assert.True(result.HasNextPage);
        Assert.Null(await local.FindPageAsync(CatalogSearch.Create("Name", page: 1, pageSize: 2), default));
        var partial = page with { Query = CatalogSearch.Create("partial"), IsComplete = false };
        await local.SavePageAsync(partial, default);
        Assert.Null(await local.FindPageAsync(partial.Query, default));
    }

    [Fact]
    public async Task Stale_is_derived_from_original_obtainment_and_page_fallback_has_a_limit()
    {
        var clock = Clock();
        var local = new InMemoryLocalGameCatalog(clock, new());
        var page = new CatalogPage(CatalogSearch.Create(), [CatalogFlow.Game("igdb-1")], false, CatalogFlow.Metadata);
        await local.SavePageAsync(page, default);
        clock.Now += TimeSpan.FromMinutes(5);
        Assert.True((await local.FindPageAsync(page.Query, default))?.Metadata.IsStale);
        clock.Now = CatalogFlow.Metadata.FetchedAt.AddMinutes(66);
        Assert.Null(await local.FindPageAsync(page.Query, default));
        var game = Assert.Single((await local.FindGamesAsync(["igdb-1"], default)).Values);
        Assert.True(game.Metadata.IsStale);
        Assert.Equal(CatalogFlow.Metadata.FetchedAt, game.Metadata.FetchedAt);
    }

    [Fact]
    public async Task Local_hosts_are_isolated_and_restart_loses_state()
    {
        var first = new InMemoryLocalGameCatalog(Clock(), new());
        var other = new InMemoryLocalGameCatalog(Clock(), new());
        await first.SaveGamesAsync([CatalogFlow.Game("igdb-1")], default);
        Assert.NotEmpty(await first.FindGamesAsync(["igdb-1"], default));
        Assert.Empty(await other.FindGamesAsync(["igdb-1"], default));
    }

    [Fact]
    public async Task Caller_mutation_does_not_change_stored_genres_or_snapshot_items()
    {
        var local = new InMemoryLocalGameCatalog(Clock(), new());
        var genres = new[] { "RPG" };
        var game = CatalogFlow.Game("igdb-1") with { Game = CatalogFlow.Game("igdb-1").Game with { Genres = genres } };
        var items = new[] { game };
        var page = new CatalogPage(CatalogSearch.Create(), items, false, CatalogFlow.Metadata);
        await local.SavePageAsync(page, default);
        genres[0] = "Changed"; items[0] = CatalogFlow.Game("igdb-2");
        var stored = await local.FindPageAsync(page.Query, default);
        Assert.NotNull(stored);
        Assert.Equal("igdb-1", Assert.Single(stored.Items).Game.Id);
        Assert.Equal("RPG", Assert.Single(stored.Items[0].Game.Genres));
    }
}
