using Tcc.Application.Identity;
using Tcc.Domain;
using Tcc.Domain.Catalog;

namespace Tcc.Application.Catalog;

public sealed record RankedCatalogGame(Game Game, double AverageRating, int RatingCount, DateTimeOffset LastRatedAt);
public sealed record RecommendedCatalogGame(Game Game, int Score, double? AverageRating, IReadOnlyList<string> MatchedGenres, IReadOnlyList<string> MatchedThemes);
public sealed record CatalogRecommendations(IReadOnlyList<RecommendedCatalogGame> Items, bool HasPreferences);
public sealed record FeaturedCatalogGame(Game? Game, string SelectionReason);
public interface IGameDiscovery
{
    Task<IReadOnlyList<RankedCatalogGame>> RankAsync(bool trending, int offset, int limit, CancellationToken ct);
    Task<CatalogRecommendations> RecommendAsync(int userId, int offset, int limit, CancellationToken ct);
    Task<FeaturedCatalogGame> FeaturedAsync(CancellationToken ct);
}

public sealed class GameDiscovery(IGameDiscovery store, ICurrentSession current, SessionAccess sessions)
{
    public Task<FeaturedCatalogGame> Featured(CancellationToken ct) => store.FeaturedAsync(ct);
    public Task<IReadOnlyList<RankedCatalogGame>> Rank(bool trending, int page, int pageSize, CancellationToken ct) =>
        store.RankAsync(trending, Offset(page, pageSize), pageSize + 1, ct);

    public async Task<CatalogRecommendations> Recommend(int page, int pageSize, CancellationToken ct)
    {
        var offset = Offset(page, pageSize);
        var session = await sessions.Require(current, ct);
        return await store.RecommendAsync(session.UserId, offset, pageSize + 1, ct);
    }

    private static int Offset(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 50 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new DomainValidationException("Paginação inválida.");
        return (page - 1) * pageSize;
    }
}
