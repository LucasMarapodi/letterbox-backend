using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Igdb;

public sealed class SingleFlightGameCatalog(IAsyncGameCatalog inner, CatalogMemoryOptions options) : IAsyncGameCatalog
{
    private readonly object gate = new();
    private readonly Dictionary<object, Task<CatalogPage>> pages = [];
    private readonly Dictionary<object, Task<IReadOnlyDictionary<string, CatalogGame>>> batches = [];
    public async Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken = default)
    {
        var page = await Run(pages, CatalogKey.From(query), () => inner.SearchAsync(query, CancellationToken.None), cancellationToken);
        return page with { Query = query };
    }
    public async Task<CatalogGame?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        (await ResolveAsync([id], cancellationToken)).GetValueOrDefault(id);
    public Task<CatalogGame?> RefreshAsync(string id, CancellationToken cancellationToken = default) => inner.RefreshAsync(id, cancellationToken);
    public async Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        var unique = CatalogIds.Unique(ids);
        if (unique.Length == 0) { cancellationToken.ThrowIfCancellationRequested(); return new Dictionary<string, CatalogGame>(); }
        var key = string.Join(',', unique.Order(StringComparer.Ordinal));
        var result = await Run(batches, key, () => inner.ResolveAsync(unique, CancellationToken.None), cancellationToken);
        return unique.Where(result.ContainsKey).ToDictionary(id => id, id => result[id], StringComparer.Ordinal);
    }
    private Task<T> Run<T>(Dictionary<object, Task<T>> flights, object key, Func<Task<T>> work, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        TaskCompletionSource<T>? owner = null;
        Task<T> shared;
        lock (gate)
        {
            if (!flights.TryGetValue(key, out var running))
            {
                if (pages.Count + batches.Count >= options.InFlightLimit) throw new CatalogIntegrationUnavailableException();
                owner = new(TaskCreationOptions.RunContinuationsAsynchronously);
                running = owner.Task; flights.Add(key, running);
            }
            shared = running;
        }
        if (owner is not null) _ = PublishAsync(flights, key, owner, work);
        return shared.WaitAsync(ct);
    }
    private async Task PublishAsync<T>(Dictionary<object, Task<T>> flights, object key, TaskCompletionSource<T> completion, Func<Task<T>> work)
    {
        try { completion.SetResult(await work()); }
        catch (Exception failure) { completion.SetException(failure); }
        finally { lock (gate) flights.Remove(key); }
    }
}
