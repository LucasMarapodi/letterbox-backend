using Tcc.Domain.Catalog;

namespace Tcc.Application.Catalog;
public enum ResourceFailureKind { NotFound, Conflict, Forbidden }
public sealed class ResourceFailure(ResourceFailureKind kind) : Exception("Operação rejeitada.")
{
    public ResourceFailureKind Kind { get; } = kind;
}
public sealed class QueryGames(IAsyncGameCatalog catalog, CatalogUsage usage)
{
    public async Task<Game> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var entry = await catalog.GetAsync(id, cancellationToken) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
        usage.Include(entry.Metadata);
        return entry.Game;
    }
    public async Task<IReadOnlyDictionary<string, Game>> ResolveAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, Game>(StringComparer.Ordinal);
        foreach (var chunk in ids.Distinct(StringComparer.Ordinal).Chunk(500))
        {
            var entries = await catalog.ResolveAsync(chunk, cancellationToken);
            foreach (var id in chunk)
            {
                if (!entries.TryGetValue(id, out var entry)) throw new ResourceFailure(ResourceFailureKind.NotFound);
                usage.Include(entry.Metadata);
                result.Add(id, entry.Game);
            }
        }
        return result;
    }
}
