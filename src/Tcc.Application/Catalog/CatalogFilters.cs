namespace Tcc.Application.Catalog;

public sealed class InvalidCatalogFilterException() : Exception("Filtro de catálogo inválido ou inexistente.");
public sealed record CatalogFilterOption(int Id, string Name);
public sealed record CatalogFilterOptions(IReadOnlyList<CatalogFilterOption> Genres, IReadOnlyList<CatalogFilterOption> Platforms);
public sealed record ExternalCatalogFilter(long Id, string Name);
public sealed record ExternalCatalogFilters(IReadOnlyList<ExternalCatalogFilter> Genres, IReadOnlyList<ExternalCatalogFilter> Platforms);
public sealed record CatalogFilterBindings(long? GenreId, long? PlatformId);
public interface ICatalogFilterStore
{
    Task<CatalogFilterOptions> ReadAsync(CancellationToken ct);
    Task SynchronizeAsync(ExternalCatalogFilters filters, CancellationToken ct);
    Task<CatalogFilterBindings> ResolveAsync(int? genreId, int? platformId, CancellationToken ct);
}
public interface IExternalCatalogFilters
{
    Task<ExternalCatalogFilters> ReadAsync(CancellationToken ct);
}
public interface ICatalogReleasePages
{
    Task<CatalogPage> ImportReleasePageAsync(CatalogSearch query, IReadOnlyList<CatalogGame> window, DateTimeOffset now, CancellationToken ct, int? windowOffset = null);
}

// A complete metadata acquisition is shared; rows remain durable in the SQL store.
public sealed class CatalogFilters(ICatalogFilterStore store, IExternalCatalogFilters external, ICatalogCache cache, TimeProvider clock) : IDisposable
{
    private readonly object gate = new();
    private Task<CatalogFilterOptions>? pending;
    private DateTimeOffset fetchedAt = DateTimeOffset.MinValue;
    private CatalogFilterOptions? cached;
    private readonly CancellationTokenSource lifetime = new();
    public Task<CatalogFilterOptions> GetAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        TaskCompletionSource<CatalogFilterOptions>? owner = null;
        Task<CatalogFilterOptions> result;
        lock (gate)
        {
            if (cached is not null && clock.GetUtcNow() < fetchedAt.AddHours(1)) return Task.FromResult(cached);
            if (pending is null) { owner = new(TaskCreationOptions.RunContinuationsAsynchronously); pending = owner.Task; }
            result = pending;
        }
        if (owner is not null) _ = Refresh(owner);
        return result.WaitAsync(ct);
    }
    private async Task Refresh(TaskCompletionSource<CatalogFilterOptions> completion)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            // Validate/read the SQL source before requesting metadata; row presence alone is not proof of complete coverage.
            await store.ReadAsync(timeout.Token);
            var rows = await external.ReadAsync(timeout.Token);
            await store.SynchronizeAsync(rows, timeout.Token);
            await cache.InvalidateAsync(timeout.Token);
            var options = await store.ReadAsync(timeout.Token);
            lock (gate) { cached = options; fetchedAt = clock.GetUtcNow(); }
            completion.TrySetResult(options);
        }
        catch (OperationCanceledException) { completion.TrySetException(new CatalogTimeoutException()); }
        catch (Exception failure) { completion.TrySetException(failure); }
        finally { lock (gate) pending = null; }
    }
    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
}
