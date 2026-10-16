using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Opinions;
namespace Tcc.Application.Opinions;
public interface IRatings
{
    Task<Rating?> FindAsync(int userId, int gameId, CancellationToken ct = default);
    Task<Rating> SetAsync(int userId, int gameId, RatingValue value, DateTimeOffset now, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, double?>> AveragesAsync(IReadOnlyList<int> gameIds, CancellationToken ct = default);
}
public sealed class PersonalRating(IRatings ratings, QueryGames games, ICurrentSession current, SessionAccess access, TimeProvider clock)
{
    public async Task<Rating?> Get(int gameId, CancellationToken ct = default)
    {
        var userId = (await access.Require(current, ct)).UserId;
        await games.GetAsync(gameId, ct);
        return await ratings.FindAsync(userId, gameId, ct);
    }
    public async Task<Rating> Set(int gameId, double value, CancellationToken ct = default)
    {
        var userId = (await access.Require(current, ct)).UserId;
        var validated = RatingValue.Create(value);
        await games.GetAsync(gameId, ct);
        await access.Require(current, ct);
        return await ratings.SetAsync(userId, gameId, validated, clock.GetUtcNow(), ct);
    }
}
public sealed class LocalRatingAverage(IRatings ratings)
{
    public async Task<double?> ForGame(int gameId, CancellationToken ct = default) => (await ratings.AveragesAsync([gameId], ct)).GetValueOrDefault(gameId);
    public async Task<IReadOnlyDictionary<int, double?>> ForGames(IEnumerable<int> gameIds, CancellationToken ct = default)
    {
        var result = new Dictionary<int, double?>();
        foreach (var chunk in gameIds.Distinct().Chunk(500))
            foreach (var entry in await ratings.AveragesAsync(chunk, ct)) result.Add(entry.Key, entry.Value);
        return result;
    }
}