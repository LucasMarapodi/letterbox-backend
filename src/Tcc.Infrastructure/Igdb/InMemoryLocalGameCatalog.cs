using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Igdb;

public sealed class InMemoryLocalGameCatalog(TimeProvider clock, CatalogMemoryOptions options) : ILocalGameCatalog
{
    public Task<CatalogPage?> FindPageAsync(CatalogSearch query, CancellationToken cancellationToken)
    { _ = clock; _ = options; _ = query; cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<CatalogPage?>(null); }
    public Task<IReadOnlyDictionary<string, CatalogGame>> FindGamesAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    { _ = ids; cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyDictionary<string, CatalogGame>>(new Dictionary<string, CatalogGame>()); }
    public Task SavePageAsync(CatalogPage page, CancellationToken cancellationToken) { _ = page; cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    public Task SaveGamesAsync(IReadOnlyList<CatalogGame> games, CancellationToken cancellationToken) { _ = games; cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
}
