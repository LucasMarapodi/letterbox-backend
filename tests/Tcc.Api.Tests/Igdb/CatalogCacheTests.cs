using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class CatalogCacheTests
{
    [Fact]
    public async Task Cache_expiry_and_eviction_leave_local_games_intact_and_local_hit_does_not_use_supplier()
    {
        var clock = new TestClock { Now = CatalogFlow.Metadata.FetchedAt };
        var options = new CatalogMemoryOptions { CacheGameLimit = 1 };
        var cache = new InMemoryCatalogCache(clock, options);
        var local = new InMemoryLocalGameCatalog(clock, options);
        var flow = new CatalogFlow();
        var catalog = new AsyncGameCatalog(cache, local, flow.External);
        await local.SaveGamesAsync([CatalogFlow.Game("igdb-1"), CatalogFlow.Game("igdb-2")], default);
        await cache.SaveGamesAsync([CatalogFlow.Game("igdb-1"), CatalogFlow.Game("igdb-2")], default);
        Assert.Empty(await cache.FindGamesAsync(["igdb-1"], default));
        Assert.NotEmpty(await cache.FindGamesAsync(["igdb-2"], default));
        clock.Now += TimeSpan.FromMinutes(31);
        Assert.Empty(await cache.FindGamesAsync(["igdb-2"], default));
        var result = await catalog.GetAsync("igdb-1");
        Assert.NotNull(result);
        Assert.True(result.Metadata.IsStale);
        Assert.Equal(CatalogFlow.Metadata.FetchedAt, result.Metadata.FetchedAt);
        Assert.DoesNotContain("external.games", flow.Events);
        var repopulated = Assert.Single((await cache.FindGamesAsync(["igdb-1"], default)).Values);
        Assert.True(repopulated.Metadata.IsStale);
        Assert.Equal(result.Metadata.FetchedAt, repopulated.Metadata.FetchedAt);
    }

    [Fact]
    public async Task Page_cache_keys_include_normalized_term_sort_page_and_size_and_preserve_valid_empty_page()
    {
        var clock = new TestClock { Now = CatalogFlow.Metadata.FetchedAt };
        var cache = new InMemoryCatalogCache(clock, new() { CachePageLimit = 1 });
        var query = CatalogSearch.Create("Name", page: 2, pageSize: 1);
        await cache.SavePageAsync(new(query, [], false, CatalogFlow.Metadata), default);
        Assert.NotNull(await cache.FindPageAsync(CatalogSearch.Create("name", page: 2, pageSize: 1), default));
        Assert.Null(await cache.FindPageAsync(CatalogSearch.Create("Name", page: 1, pageSize: 1), default));
        Assert.Null(await cache.FindPageAsync(CatalogSearch.Create("Name", "name-desc", 2, 1), default));
        clock.Now += TimeSpan.FromMinutes(5);
        Assert.True((await cache.FindPageAsync(query, default))?.Metadata.IsStale);
        clock.Now += TimeSpan.FromMinutes(61);
        Assert.Null(await cache.FindPageAsync(query, default));
    }

    [Fact]
    public async Task Concurrent_same_page_shares_supplier_call_and_records_data_before_caching()
    {
        var clock = new TestClock { Now = CatalogFlow.Metadata.FetchedAt };
        var options = new CatalogMemoryOptions();
        var cache = new InMemoryCatalogCache(clock, options);
        var local = new InMemoryLocalGameCatalog(clock, options);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var supplier = new BlockingSupplier(release.Task);
        var catalog = new SingleFlightGameCatalog(new AsyncGameCatalog(cache, local, supplier), options);
        var query = CatalogSearch.Create("Game");
        var requests = Enumerable.Range(0, 16).Select(_ => catalog.SearchAsync(query)).ToArray();
        try { Assert.Equal(1, supplier.Calls); }
        finally { release.TrySetResult(); }
        await Task.WhenAll(requests);
        Assert.NotNull(await local.FindPageAsync(query, default));
        Assert.NotNull(await cache.FindPageAsync(query, default));
        await catalog.SearchAsync(query);
        Assert.Equal(1, supplier.Calls);
    }

    [Fact]
    public async Task Cancelling_one_waiter_does_not_cancel_shared_page_and_failed_supplier_does_not_cache_empty_success()
    {
        var clock = new TestClock { Now = CatalogFlow.Metadata.FetchedAt };
        var options = new CatalogMemoryOptions();
        var cache = new InMemoryCatalogCache(clock, options);
        var local = new InMemoryLocalGameCatalog(clock, options);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var supplier = new BlockingSupplier(release.Task);
        var catalog = new SingleFlightGameCatalog(new AsyncGameCatalog(cache, local, supplier), options);
        var query = CatalogSearch.Create("Game");
        using var cancel = new CancellationTokenSource();
        var abandoned = catalog.SearchAsync(query, cancel.Token);
        var remaining = catalog.SearchAsync(query);
        cancel.Cancel();
        try { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned); }
        finally { release.TrySetResult(); }
        await remaining;
        Assert.Equal(1, supplier.Calls);
        supplier.Fail = true;
        var missing = CatalogSearch.Create("missing");
        await Assert.ThrowsAsync<CatalogIntegrationUnavailableException>(() => catalog.SearchAsync(missing));
        Assert.Null(await cache.FindPageAsync(missing, default));
        Assert.Null(await local.FindPageAsync(missing, default));
    }

    [Fact]
    public async Task Full_singleflight_book_does_not_block_a_known_local_page()
    {
        var clock = new TestClock { Now = CatalogFlow.Metadata.FetchedAt };
        var options = new CatalogMemoryOptions { InFlightLimit = 1 };
        var cache = new InMemoryCatalogCache(clock, options);
        var local = new InMemoryLocalGameCatalog(clock, options);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var supplier = new BlockingSupplier(release.Task);
        var catalog = new SingleFlightGameCatalog(new AsyncGameCatalog(cache, local, supplier), options);
        var warm = CatalogSearch.Create("warm");
        await local.SavePageAsync(new(warm, [CatalogFlow.Game("igdb-2")], false, CatalogFlow.Metadata), default);
        var pending = catalog.SearchAsync(CatalogSearch.Create("cold"));
        try { Assert.Equal("igdb-2", Assert.Single((await catalog.SearchAsync(warm)).Items).Game.Id); }
        finally { release.TrySetResult(); }
        await pending;
        Assert.Equal(1, supplier.Calls);
    }
}

internal sealed class BlockingSupplier(Task release) : IExternalGameCatalog
{
    public int Calls;
    public bool Fail { get; set; }
    public async Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        await release.WaitAsync(cancellationToken);
        if (Fail) throw new CatalogIntegrationUnavailableException();
        return new(query, [CatalogFlow.Game("igdb-1")], false, CatalogFlow.Metadata);
    }
    public async Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls); await release.WaitAsync(cancellationToken);
        if (Fail) throw new CatalogIntegrationUnavailableException();
        return ids.ToDictionary(id => id, CatalogFlow.Game, StringComparer.Ordinal);
    }
}
