using Tcc.Application.Catalog;
using Tcc.Infrastructure.Persistence.Repositories;

namespace Tcc.Api.Configuration;

// Shared single-flight/cache services lease a scoped SQL unit for each operation.
internal sealed class ScopedSqlLocalGameCatalog(IServiceScopeFactory scopes) : ILocalGameCatalog
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
}
