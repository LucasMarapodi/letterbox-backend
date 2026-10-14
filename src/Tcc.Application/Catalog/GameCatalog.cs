using Tcc.Domain.Catalog;
using Tcc.Domain.Identity;

namespace Tcc.Application.Catalog;

public interface IGameCatalog
{
    IReadOnlyList<Game> All();
    Game? Find(string id);
}

public enum ResourceFailureKind { NotFound, Conflict, Forbidden }
public sealed class ResourceFailure(ResourceFailureKind kind) : Exception("Operação rejeitada.")
{
    public ResourceFailureKind Kind { get; } = kind;
}

public sealed class QueryGames(IGameCatalog catalog, IAsyncGameCatalog asyncCatalog, CatalogUsage usage)
{
    public async Task<Game> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var entry = await asyncCatalog.GetAsync(id, cancellationToken) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
        usage.Include(entry.Metadata);
        return entry.Game;
    }
    public async Task<IReadOnlyDictionary<string, Game>> ResolveAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, Game>(StringComparer.Ordinal);
        foreach (var chunk in ids.Distinct(StringComparer.Ordinal).Chunk(500))
        {
            var entries = await asyncCatalog.ResolveAsync(chunk, cancellationToken);
            foreach (var id in chunk)
            {
                if (!entries.TryGetValue(id, out var entry)) throw new ResourceFailure(ResourceFailureKind.NotFound);
                usage.Include(entry.Metadata);
                result.Add(id, entry.Game);
            }
        }
        return result;
    }
    public Game Get(string id) => catalog.Find(id) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);

    public IReadOnlyList<Game> List(string? term, string? sort)
    {
        sort ??= "name-asc";
        if (sort is not ("name-asc" or "name-desc")) throw new DomainValidationException("Ordenação inválida.");
        var games = catalog.All().Where(game => game.Title.Contains(term?.Trim() ?? "", StringComparison.OrdinalIgnoreCase));
        return (sort == "name-asc" ? games.OrderBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            : games.OrderByDescending(game => game.Title, StringComparer.OrdinalIgnoreCase))
            .ThenBy(game => game.Id, StringComparer.Ordinal).ToArray();
    }
}
