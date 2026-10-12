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

public sealed class QueryGames(IGameCatalog catalog)
{
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
