using Tcc.Application.Catalog;
using Tcc.Infrastructure.Persistence.Repositories;

namespace Tcc.Api.Catalog.Adapters;

// Shared single-flight/cache services lease a scoped SQL unit for each operation.
internal sealed class ScopedSqlLocalGameCatalog(IServiceScopeFactory scopes) : ILocalGameCatalog, ICatalogFilterStore, ICatalogReleasePages
{
    private async Task<T> Run<T>(Func<PostgresGameCatalog, Task<T>> operation)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await operation(scope.ServiceProvider.GetRequiredService<PostgresGameCatalog>());
    }
    public Task<CatalogPage?> FindPageAsync(CatalogSearch query, CancellationToken ct) => Run(store => store.FindPageAsync(query, ct));
    public Task<IReadOnlyDictionary<int, CatalogGame>> FindGamesAsync(IReadOnlyList<int> ids, CancellationToken ct) => Run(store => store.FindGamesAsync(ids, ct));
    public Task<IReadOnlyDictionary<long, CatalogGame>> FindByIgdbIdsAsync(IReadOnlyList<long> ids, CancellationToken ct) => Run(store => store.FindByIgdbIdsAsync(ids, ct));
    public Task<IReadOnlyList<CatalogGame>> UpsertAsync(IReadOnlyList<CatalogGame> games, CancellationToken ct) => Run(store => store.UpsertAsync(games, ct));
    public async Task SaveGamesAsync(IReadOnlyList<CatalogGame> games, CancellationToken ct) => _ = await UpsertAsync(games, ct);
    public async Task SavePageAsync(CatalogPage page, CancellationToken ct) => await Run(async store => { await store.SavePageAsync(page, ct); return true; });
    public Task<CatalogFilterOptions> ReadAsync(CancellationToken ct) => Run(store => store.ReadAsync(ct));
    public Task SynchronizeAsync(ExternalCatalogFilters filters, CancellationToken ct) => Run(async store => { await store.SynchronizeAsync(filters, ct); return true; });
    public Task<CatalogFilterBindings> ResolveAsync(int? genreId, int? platformId, CancellationToken ct) => Run(store => store.ResolveAsync(genreId, platformId, ct));
    public Task<CatalogPage> ImportReleasePageAsync(CatalogSearch query, IReadOnlyList<CatalogGame> window, DateTimeOffset now, CancellationToken ct, int? windowOffset = null) =>
        Run(store => store.ImportReleasePageAsync(query, window, now, ct, windowOffset));
}
