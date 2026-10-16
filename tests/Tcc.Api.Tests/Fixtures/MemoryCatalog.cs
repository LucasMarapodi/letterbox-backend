using Microsoft.Extensions.DependencyInjection;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Fixtures;

public static class TestCatalogRegistration
{
    public static IServiceCollection AddTestCatalog(this IServiceCollection services)
    { services.AddSingleton<ILocalGameCatalog, TestLocalGameCatalog>(); return services; }
}
internal sealed class TestLocalGameCatalog(TimeProvider clock, CatalogMemoryOptions options) : ILocalGameCatalog
{
    private readonly InMemoryLocalGameCatalog store = new(clock, options);
    private readonly Dictionary<long, int> externalIds = [];
    private readonly object gate = new();
    private int next = 10000;
    public Task<CatalogPage?> FindPageAsync(CatalogSearch query, CancellationToken ct) => store.FindPageAsync(query, ct);
    public Task<IReadOnlyDictionary<int, CatalogGame>> FindGamesAsync(IReadOnlyList<int> ids, CancellationToken ct) => store.FindGamesAsync(ids, ct);
    public Task SavePageAsync(CatalogPage page, CancellationToken ct) => store.SavePageAsync(page, ct);
    public Task SaveGamesAsync(IReadOnlyList<CatalogGame> games, CancellationToken ct) => store.SaveGamesAsync(games, ct);
    public async Task<IReadOnlyList<CatalogGame>> UpsertAsync(IReadOnlyList<CatalogGame> games, CancellationToken ct)
    {
        CatalogGame[] saved;
        lock (gate) saved = games.Select(item =>
        {
            if (!externalIds.TryGetValue(item.Game.IgdbId, out var id)) externalIds[item.Game.IgdbId] = id = item.Game.Id > 0 ? item.Game.Id : ++next;
            return item with { Game = item.Game with { Id = id } };
        }).ToArray();
        await store.SaveGamesAsync(saved, ct);
        return saved;
    }
    public async Task<IReadOnlyDictionary<long, CatalogGame>> FindByIgdbIdsAsync(IReadOnlyList<long> ids, CancellationToken ct)
    {
        int[] local;
        lock (gate) local = ids.Where(externalIds.ContainsKey).Select(id => externalIds[id]).ToArray();
        return (await store.FindGamesAsync(local, ct)).Values.ToDictionary(item => item.Game.IgdbId);
    }
}
