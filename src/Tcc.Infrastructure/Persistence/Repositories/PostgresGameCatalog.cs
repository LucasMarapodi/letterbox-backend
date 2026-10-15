using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresGameCatalog : ILocalGameCatalog
{
    public PostgresGameCatalog(Lazy<TccDbContext> context, TimeProvider clock, CatalogMemoryOptions options) { _ = context; _ = clock; _ = options; }
    public Task<CatalogPage?> FindPageAsync(CatalogSearch query, CancellationToken ct) => Task.FromResult<CatalogPage?>(null);
    public Task<IReadOnlyDictionary<int, CatalogGame>> FindGamesAsync(IReadOnlyList<int> ids, CancellationToken ct) => Task.FromResult<IReadOnlyDictionary<int, CatalogGame>>(new Dictionary<int, CatalogGame>());
    public Task<IReadOnlyDictionary<long, CatalogGame>> FindByIgdbIdsAsync(IReadOnlyList<long> ids, CancellationToken ct) => Task.FromResult<IReadOnlyDictionary<long, CatalogGame>>(new Dictionary<long, CatalogGame>());
    public Task SavePageAsync(CatalogPage page, CancellationToken ct) => Task.CompletedTask;
    public Task SaveGamesAsync(IReadOnlyList<CatalogGame> games, CancellationToken ct) => throw new DatabaseUnavailableException();
    public Task<IReadOnlyList<CatalogGame>> UpsertAsync(IReadOnlyList<CatalogGame> games, CancellationToken ct) => throw new DatabaseUnavailableException();
}
