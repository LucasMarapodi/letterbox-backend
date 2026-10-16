using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Igdb;

public sealed class SingleFlightGameCatalog(IAsyncGameCatalog inner, CatalogMemoryOptions options) : IAsyncGameCatalog
{
    private readonly object gate = new();
    private readonly Dictionary<object, Task<CatalogPage>> pages = [];
    private readonly Dictionary<object, Task<IReadOnlyDictionary<string, CatalogGame>>> batches = [];
    private readonly Dictionary<object, Task<CatalogGame?>> refreshes = [];
    private sealed class CapacityException : Exception;
    public async Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken = default)
    {
        CatalogPage page;
        try { page = await Run(pages, CatalogKey.From(query), () => inner.SearchAsync(query, CancellationToken.None), cancellationToken); }
        catch (CapacityException)
        {
            if (inner is IKnownCatalogReader known && await known.FindKnownPageAsync(query, cancellationToken) is CatalogPage available) page = available;
            else throw new CatalogIntegrationUnavailableException();
        }
        return page with { Query = query };
    }
    public async Task<CatalogGame?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        (await ResolveAsync([id], cancellationToken)).GetValueOrDefault(id);
    public async Task<CatalogGame?> RefreshAsync(string id, CancellationToken cancellationToken = default)
    {
        CatalogIds.Parse(id);
        try { return await Run(refreshes, id, () => inner.RefreshAsync(id, CancellationToken.None), cancellationToken); }
        catch (CapacityException)
        {
            if (inner is IKnownCatalogReader known && (await known.FindKnownGamesAsync([id], cancellationToken)).TryGetValue(id, out var available)) return available;
            throw new CatalogIntegrationUnavailableException();
        }
    }
    public async Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        var unique = CatalogIds.Unique(ids);
        if (unique.Length == 0) { cancellationToken.ThrowIfCancellationRequested(); return new Dictionary<string, CatalogGame>(); }
        var key = string.Join(',', unique.Order(StringComparer.Ordinal));
        IReadOnlyDictionary<string, CatalogGame> result;
        try { result = await Run(batches, key, () => inner.ResolveAsync(unique, CancellationToken.None), cancellationToken); }
        catch (CapacityException)
        {
            if (inner is not IKnownCatalogReader known) throw new CatalogIntegrationUnavailableException();
            result = await known.FindKnownGamesAsync(unique, cancellationToken);
            if (result.Count != unique.Length) throw new CatalogIntegrationUnavailableException();
        }
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
                if (pages.Count + batches.Count + refreshes.Count >= options.InFlightLimit) throw new CapacityException();
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
