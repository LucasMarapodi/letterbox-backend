using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Igdb;

public sealed class InMemoryCatalogCache : ICatalogCache
{
    private readonly TimeProvider clock;
    private readonly CatalogMemoryOptions options;
    private readonly object gate = new();
    private readonly BoundedCatalogMap<CatalogKey, CatalogPage> pages;
    private readonly BoundedCatalogMap<string, Entry> games;
    private sealed record Entry(CatalogGame Data, DateTimeOffset ExpiresAt);
    public InMemoryCatalogCache(TimeProvider clock, CatalogMemoryOptions options)
    { options.Validate(); this.clock = clock; this.options = options; pages = new(options.CachePageLimit); games = new(options.CacheGameLimit); }
    public Task<CatalogPage?> FindPageAsync(CatalogSearch query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var key = CatalogKey.From(query);
            if (!pages.TryGet(key, out var page)) return Task.FromResult<CatalogPage?>(null);
            var now = clock.GetUtcNow();
            if (CatalogStorage.PageExpired(page.Metadata, now, options))
            { pages.Remove(key); return Task.FromResult<CatalogPage?>(null); }
            var items = page.Items.Select(item => CatalogStorage.Read(item, now, options)).ToArray();
            return Task.FromResult<CatalogPage?>(CatalogStorage.ReadPage(page, query, items, now, options));
        }
    }
    public Task<IReadOnlyDictionary<string, CatalogGame>> FindGamesAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unique = CatalogIds.Unique(ids);
        var result = new Dictionary<string, CatalogGame>(StringComparer.Ordinal);
        lock (gate)
        {
            var now = clock.GetUtcNow();
            foreach (var id in unique)
            {
                if (!games.TryGet(id, out var entry)) continue;
                if (now >= entry.ExpiresAt) { games.Remove(id); continue; }
                result.Add(id, CatalogStorage.Read(entry.Data, now, options));
            }
        }
        return Task.FromResult<IReadOnlyDictionary<string, CatalogGame>>(result);
    }
    public Task SavePageAsync(CatalogPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!page.IsComplete) return Task.CompletedTask;
        CatalogStorage.ValidatePage(page);
        var frozen = page with { Items = Array.AsReadOnly(page.Items.Select(CatalogStorage.Freeze).ToArray()) };
        lock (gate)
        {
            var key = CatalogKey.From(page.Query);
            if (!pages.TryGet(key, out var existing) || existing.Metadata.FetchedAt <= page.Metadata.FetchedAt) pages.Set(key, frozen);
        }
        return Task.CompletedTask;
    }
    public Task SaveGamesAsync(IReadOnlyList<CatalogGame> entries, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var frozen = entries.Select(CatalogStorage.Freeze).ToArray();
        lock (gate)
            foreach (var item in frozen)
                if (!games.TryGet(item.Game.Id, out var existing) || existing.Data.Metadata.FetchedAt <= item.Metadata.FetchedAt)
                    games.Set(item.Game.Id, new(item, clock.GetUtcNow() + options.DetailFreshFor));
        return Task.CompletedTask;
    }
    // Controlled maintenance on the infrastructure instance; never exposed as an HTTP endpoint.
    public bool EvictGame(string id)
    {
        CatalogIds.Parse(id);
        lock (gate)
        {
            if (!games.TryGet(id, out _)) return false;
            games.Remove(id);
            return true;
        }
    }
}
