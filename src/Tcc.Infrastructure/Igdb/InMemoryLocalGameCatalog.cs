using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Igdb;

public sealed class InMemoryLocalGameCatalog : ILocalGameCatalog
{
    private readonly TimeProvider clock;
    private readonly CatalogMemoryOptions options;
    private readonly object gate = new();
    private readonly Dictionary<string, CatalogGame> games = new(StringComparer.Ordinal);
    private readonly BoundedCatalogMap<CatalogKey, Snapshot> snapshots;
    private sealed record Snapshot(CatalogPage Page, string[] Ids);
    public InMemoryLocalGameCatalog(TimeProvider clock, CatalogMemoryOptions options)
    { options.Validate(); this.clock = clock; this.options = options; snapshots = new(options.SnapshotLimit); }
    public Task<CatalogPage?> FindPageAsync(CatalogSearch query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var key = CatalogKey.From(query);
            if (!snapshots.TryGet(key, out var snapshot)) return Task.FromResult<CatalogPage?>(null);
            var now = clock.GetUtcNow();
            if (CatalogStorage.PageExpired(snapshot.Page.Metadata, now, options))
            { snapshots.Remove(key); return Task.FromResult<CatalogPage?>(null); }
            var items = snapshot.Ids.Select(id => CatalogStorage.Read(games[id], now, options)).ToArray();
            return Task.FromResult<CatalogPage?>(CatalogStorage.ReadPage(snapshot.Page, query, items, now, options));
        }
    }
    public Task<IReadOnlyDictionary<string, CatalogGame>> FindGamesAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unique = CatalogIds.Unique(ids);
        lock (gate) return Task.FromResult<IReadOnlyDictionary<string, CatalogGame>>(unique.Where(games.ContainsKey)
            .ToDictionary(id => id, id => CatalogStorage.Read(games[id], clock.GetUtcNow(), options), StringComparer.Ordinal));
    }
    public Task SavePageAsync(CatalogPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!page.IsComplete) return Task.CompletedTask;
        CatalogStorage.ValidatePage(page);
        var frozen = page.Items.Select(CatalogStorage.Freeze).ToArray();
        lock (gate)
        {
            foreach (var item in frozen) Upsert(item);
            var key = CatalogKey.From(page.Query);
            if (!snapshots.TryGet(key, out var existing) || existing.Page.Metadata.FetchedAt <= page.Metadata.FetchedAt)
                snapshots.Set(key, new(page with { Items = Array.Empty<CatalogGame>() }, frozen.Select(item => item.Game.Id).ToArray()));
        }
        return Task.CompletedTask;
    }
    public Task SaveGamesAsync(IReadOnlyList<CatalogGame> entries, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var frozen = entries.Select(CatalogStorage.Freeze).ToArray();
        lock (gate) foreach (var item in frozen) Upsert(item);
        return Task.CompletedTask;
    }
    private void Upsert(CatalogGame item)
    { if (!games.TryGetValue(item.Game.Id, out var existing) || existing.Metadata.FetchedAt <= item.Metadata.FetchedAt) games[item.Game.Id] = item; }
}
