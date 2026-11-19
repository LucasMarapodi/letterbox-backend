using Microsoft.EntityFrameworkCore;
using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresGameDiscovery(Lazy<TccDbContext> context, ILocalGameCatalog catalog) : IGameDiscovery
{
    public Task<IReadOnlyList<RankedCatalogGame>> RankAsync(bool trending, int offset, int limit, CancellationToken ct) =>
        OpinionStorage.Run<IReadOnlyList<RankedCatalogGame>>(async () =>
        {
            var query = context.Value.Database.SqlQuery<RatingRow>($"""
                SELECT jogo_id AS "GameId", AVG(nota) AS "AverageRating", COUNT(*)::int AS "RatingCount",
                       MAX(atualizado_em) AS "LastRatedAt" FROM avaliacoes GROUP BY jogo_id
                """);
            var ordered = trending
                ? query.OrderByDescending(row => row.LastRatedAt).ThenBy(row => row.GameId)
                : query.OrderByDescending(row => row.AverageRating).ThenByDescending(row => row.RatingCount)
                    .ThenByDescending(row => row.LastRatedAt).ThenBy(row => row.GameId);
            var rows = await ordered.Skip(offset).Take(limit).ToArrayAsync(ct);
            var games = await catalog.FindGamesAsync(rows.Select(row => row.GameId).ToArray(), ct);
            return rows.Select(row => new RankedCatalogGame(games[row.GameId].Game, row.AverageRating, row.RatingCount, row.LastRatedAt)).ToArray();
        });

    public Task<CatalogRecommendations> RecommendAsync(int userId, int offset, int limit, CancellationToken ct) =>
        OpinionStorage.Run(async () =>
        {
            var db = context.Value;
            var favorites = db.GameFavorites.Where(favorite => favorite.UserId == userId).Select(favorite => favorite.GameId);
            var hasPreferences = await db.GameGenres.AnyAsync(link => favorites.Contains(link.GameId), ct)
                || await db.GameThemes.AnyAsync(link => favorites.Contains(link.GameId), ct);
            if (!hasPreferences) return new CatalogRecommendations([], false);
            var rows = await db.Database.SqlQuery<RecommendationRow>($"""
                WITH favorite_games AS (SELECT jogo_id FROM jogos_favoritos WHERE usuario_id={userId}),
                preferred_genres AS (SELECT DISTINCT genero_id FROM jogos_generos WHERE jogo_id IN (SELECT jogo_id FROM favorite_games)),
                preferred_themes AS (SELECT DISTINCT tema_id FROM jogos_temas WHERE jogo_id IN (SELECT jogo_id FROM favorite_games)),
                genre_matches AS (
                    SELECT l.jogo_id, COUNT(*)::int AS count, ARRAY_AGG(g.nome ORDER BY g.id) AS names
                    FROM jogos_generos l JOIN preferred_genres p ON p.genero_id=l.genero_id JOIN generos g ON g.id=l.genero_id
                    GROUP BY l.jogo_id),
                theme_matches AS (
                    SELECT l.jogo_id, COUNT(*)::int AS count, ARRAY_AGG(t.nome ORDER BY t.id) AS names
                    FROM jogos_temas l JOIN preferred_themes p ON p.tema_id=l.tema_id JOIN temas t ON t.id=l.tema_id
                    GROUP BY l.jogo_id),
                averages AS (SELECT jogo_id, AVG(nota) AS average FROM avaliacoes GROUP BY jogo_id)
                SELECT j.id AS "GameId", (COALESCE(g.count,0)+COALESCE(t.count,0)) AS "Score",
                       a.average AS "AverageRating", COALESCE(g.names,ARRAY[]::text[]) AS "MatchedGenres",
                       COALESCE(t.names,ARRAY[]::text[]) AS "MatchedThemes"
                FROM jogos j LEFT JOIN genre_matches g ON g.jogo_id=j.id LEFT JOIN theme_matches t ON t.jogo_id=j.id
                LEFT JOIN averages a ON a.jogo_id=j.id
                WHERE COALESCE(g.count,0)+COALESCE(t.count,0)>0
                  AND NOT EXISTS(SELECT 1 FROM favorite_games f WHERE f.jogo_id=j.id)
                  AND NOT EXISTS(SELECT 1 FROM avaliacoes r WHERE r.usuario_id={userId} AND r.jogo_id=j.id)
                ORDER BY "Score" DESC,a.average DESC NULLS LAST,j.id ASC
                OFFSET {offset} LIMIT {limit}
                """).ToArrayAsync(ct);
            var games = await catalog.FindGamesAsync(rows.Select(row => row.GameId).ToArray(), ct);
            return new CatalogRecommendations(rows.Select(row => new RecommendedCatalogGame(games[row.GameId].Game,
                row.Score, row.AverageRating, row.MatchedGenres, row.MatchedThemes)).ToArray(), true);
        });

    private sealed record RatingRow(int GameId, double AverageRating, int RatingCount, DateTimeOffset LastRatedAt);
    private sealed record RecommendationRow(int GameId, int Score, double? AverageRating, string[] MatchedGenres, string[] MatchedThemes);
}
