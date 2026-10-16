using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class CatalogFilterConcurrencyTests
{
    [Fact]
    public async Task Metadata_sync_is_shared_and_canceling_one_waiter_does_not_cancel_others_or_cache()
    {
        var probe = new OptionsProbe();
        var clock = new TestClock();
        using var options = new CatalogFilters(probe, probe, new InMemoryCatalogCache(clock, new()), clock);
        using var canceled = new CancellationTokenSource();
        var first = options.GetAsync(canceled.Token);
        var second = options.GetAsync(default);
        Assert.Equal(1, probe.Calls);
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        probe.Release.SetResult();
        var restored = await second;
        Assert.Single(restored.Genres);
        Assert.Equal(1, probe.Writes);
        Assert.Same(restored, await options.GetAsync(default));
        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public async Task Distinct_filters_do_not_share_a_single_flight()
    {
        var inner = new FlightProbe();
        var catalog = new SingleFlightGameCatalog(inner, new());
        var first = catalog.SearchAsync(CatalogSearch.Create("Game", genreId: 1));
        var second = catalog.SearchAsync(CatalogSearch.Create("Game", genreId: 2));
        var third = catalog.SearchAsync(CatalogSearch.Create("Game", platformId: 1));
        Assert.Equal(3, inner.Calls);
        inner.Release.SetResult();
        await Task.WhenAll(first, second, third);
    }

    private sealed class OptionsProbe : ICatalogFilterStore, IExternalCatalogFilters
    {
        public int Calls { get; private set; }
        public int Writes { get; private set; }
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<CatalogFilterOptions> ReadAsync(CancellationToken ct) => Task.FromResult(new CatalogFilterOptions([new(1, "RPG")], [new(1, "PC")]));
        public Task SynchronizeAsync(ExternalCatalogFilters filters, CancellationToken ct) { Writes++; return Task.CompletedTask; }
        public Task<CatalogFilterBindings> ResolveAsync(int? genreId, int? platformId, CancellationToken ct) => throw new NotSupportedException();
        async Task<ExternalCatalogFilters> IExternalCatalogFilters.ReadAsync(CancellationToken ct)
        {
            Calls++;
            await Release.Task.WaitAsync(ct);
            return new([new(3_000_000_001, "RPG")], [new(3_000_000_002, "PC")]);
        }
    }
    private sealed class FlightProbe : IAsyncGameCatalog
    {
        public int Calls { get; private set; }
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken ct)
        {
            Calls++;
            await Release.Task.WaitAsync(ct);
            return new(query, [], false, new(DateTimeOffset.UtcNow));
        }
        public Task<CatalogGame?> GetAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<CatalogGame?> RefreshAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, CatalogGame>> ResolveAsync(IReadOnlyList<int> ids, CancellationToken ct) => throw new NotSupportedException();
    }
}
