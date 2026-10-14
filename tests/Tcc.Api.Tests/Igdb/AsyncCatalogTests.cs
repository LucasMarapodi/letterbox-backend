using Tcc.Application.Catalog;
using Tcc.Domain.Catalog;
using Tcc.Domain.Identity;

namespace Tcc.Api.Tests.Igdb;

public sealed class AsyncCatalogTests
{
    [Theory]
    [InlineData("cache")]
    [InlineData("local")]
    [InlineData("external")]
    public async Task Search_returns_early_and_stores_external_discovery_locally_before_cache(string hit)
    {
        var flow = new CatalogFlow();
        var query = CatalogSearch.Create("name", page: 2);
        var page = new CatalogPage(query, [CatalogFlow.Game("igdb-1")], true, CatalogFlow.Metadata);
        if (hit == "cache") flow.Cache.Page = page;
        if (hit == "local") flow.Local.Page = page;
        flow.External.Page = page;
        Assert.Same(page, await flow.Catalog.SearchAsync(query));
        string[] expected = hit switch
        {
            "cache" => ["cache.page"],
            "local" => ["cache.page", "local.page", "cache.save-games", "cache.save-page"],
            _ => ["cache.page", "local.page", "external.page", "local.save-games", "local.save-page", "cache.save-games", "cache.save-page"]
        };
        Assert.Equal(expected, flow.Events);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Incomplete_or_stale_local_page_does_not_claim_global_coverage(bool complete, bool stale)
    {
        var flow = new CatalogFlow();
        var query = CatalogSearch.Create();
        flow.Local.Page = new(query, [CatalogFlow.Game("igdb-1")], false, CatalogFlow.Metadata with { IsStale = stale }, complete);
        flow.External.Page = new(query, [], false, CatalogFlow.Metadata);
        Assert.Same(flow.External.Page, await flow.Catalog.SearchAsync(query));
        Assert.Contains("external.page", flow.Events);
    }

    [Fact]
    public async Task Batch_queries_only_missing_unique_ids_and_preserves_original_metadata()
    {
        var flow = new CatalogFlow();
        var cached = CatalogFlow.Game("igdb-1");
        var local = CatalogFlow.Game("igdb-2") with { Metadata = CatalogFlow.Metadata with { IsStale = true } };
        flow.Cache.Games.Add(cached.Game.Id, cached);
        flow.Local.Games.Add(local.Game.Id, local);
        flow.External.Games.Add("igdb-3", CatalogFlow.Game("igdb-3"));
        var result = await flow.Catalog.ResolveAsync(["igdb-1", "igdb-2", "igdb-3", "igdb-2"]);
        Assert.Equal(new[] { "igdb-1", "igdb-2", "igdb-3" }, result.Keys);
        Assert.Same(cached, result["igdb-1"]);
        Assert.Same(local, result["igdb-2"]);
        Assert.Equal(new[] { "igdb-2", "igdb-3" }, flow.Local.Requested);
        Assert.Equal(new[] { "igdb-3" }, flow.External.Requested);
        Assert.True(flow.Events.IndexOf("local.save-games") < flow.Events.LastIndexOf("cache.save-games"));
    }

    [Fact]
    public async Task Cached_and_known_local_details_do_not_access_supplier_and_missing_detail_is_null()
    {
        var flow = new CatalogFlow();
        flow.Local.Games.Add("igdb-1", CatalogFlow.Game("igdb-1"));
        Assert.NotNull(await flow.Catalog.GetAsync("igdb-1"));
        Assert.DoesNotContain("external.games", flow.Events);
        Assert.Null(await flow.Catalog.GetAsync("igdb-2"));
        Assert.Contains("external.games", flow.Events);
    }

    [Fact]
    public async Task Cancellation_is_forwarded_and_stops_before_the_next_layer()
    {
        var flow = new CatalogFlow();
        using var cancellation = new CancellationTokenSource();
        flow.Local.OnRead = () => cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => flow.Catalog.ResolveAsync(["igdb-1"], cancellation.Token));
        Assert.Equal(cancellation.Token, flow.Cache.Token);
        Assert.Equal(cancellation.Token, flow.Local.Token);
        Assert.DoesNotContain("external.games", flow.Events);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void Invalid_pagination_is_rejected_before_querying(int page, int size) =>
        Assert.Throws<DomainValidationException>(() => CatalogSearch.Create(page: page, pageSize: size));

    [Fact]
    public async Task Legacy_or_malformed_ids_are_not_silently_mapped_to_real_games()
    {
        var flow = new CatalogFlow();
        await Assert.ThrowsAsync<DomainValidationException>(() => flow.Catalog.GetAsync("game-1"));
        Assert.Empty(flow.Events);
    }
}

internal sealed class CatalogFlow
{
    public static CatalogMetadata Metadata { get; } = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
    public static CatalogGame Game(string id) => new(new Game(id, "Name", "", null, [], [], null), Metadata);
    public List<string> Events { get; } = [];
    public Store Cache { get; }
    public Store Local { get; }
    public Supplier External { get; }
    public AsyncGameCatalog Catalog { get; }
    public CatalogFlow()
    {
        Cache = new("cache", Events); Local = new("local", Events); External = new(Events);
        Catalog = new(Cache, Local, External);
    }
    public sealed class Store(string name, List<string> events) : ICatalogCache, ILocalGameCatalog
    {
        public CatalogPage? Page { get; set; }
        public Dictionary<string, CatalogGame> Games { get; } = [];
        public IReadOnlyList<string> Requested { get; private set; } = [];
        public CancellationToken Token { get; private set; }
        public Action? OnRead { get; set; }
        public Task<CatalogPage?> FindPageAsync(CatalogSearch query, CancellationToken ct)
        { Token = ct; events.Add(name + ".page"); OnRead?.Invoke(); return Task.FromResult(Page); }
        public Task<IReadOnlyDictionary<string, CatalogGame>> FindGamesAsync(IReadOnlyList<string> ids, CancellationToken ct)
        { Token = ct; Requested = ids; events.Add(name + ".games"); OnRead?.Invoke(); return Task.FromResult<IReadOnlyDictionary<string, CatalogGame>>(Games.Where(pair => ids.Contains(pair.Key)).ToDictionary()); }
        public Task SavePageAsync(CatalogPage page, CancellationToken ct) { events.Add(name + ".save-page"); return Task.CompletedTask; }
        public Task SaveGamesAsync(IReadOnlyList<CatalogGame> games, CancellationToken ct) { events.Add(name + ".save-games"); return Task.CompletedTask; }
    }
    public sealed class Supplier(List<string> events) : IExternalGameCatalog
    {
        public CatalogPage? Page { get; set; }
        public Dictionary<string, CatalogGame> Games { get; } = [];
        public IReadOnlyList<string> Requested { get; private set; } = [];
        public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); events.Add("external.page"); return Task.FromResult(Page ?? new(query, [], false, Metadata)); }
        public Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Requested = ids; events.Add("external.games"); return Task.FromResult<IReadOnlyDictionary<string, CatalogGame>>(Games.Where(pair => ids.Contains(pair.Key)).ToDictionary()); }
    }
}
