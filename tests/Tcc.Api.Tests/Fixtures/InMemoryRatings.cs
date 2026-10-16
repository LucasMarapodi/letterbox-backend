using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;
namespace Tcc.Infrastructure.Opinions;
public sealed class InMemoryRatings : IRatings
{
    private readonly object gate = new();
    private readonly Dictionary<(int User, int Game), Rating> ratings = [];
    private int next;
    public Rating? Find(int userId, int gameId) { lock (gate) return ratings.GetValueOrDefault((userId, gameId)); }
    public IReadOnlyList<Rating> ForGame(int gameId) { lock (gate) return ratings.Values.Where(item => item.GameId == gameId).ToArray(); }
    public Rating Set(int userId, int gameId, RatingValue value, DateTimeOffset? now = null)
    {
        lock (gate)
        {
            var existing = Find(userId, gameId); var timestamp = now ?? DateTimeOffset.UtcNow;
            return ratings[(userId, gameId)] = new(existing?.Id ?? ++next, userId, gameId, value, existing?.CreatedAt ?? timestamp, timestamp);
        }
    }
    public Task<Rating?> FindAsync(int userId, int gameId, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); return Task.FromResult(Find(userId, gameId)); }
    public Task<Rating> SetAsync(int userId, int gameId, RatingValue value, DateTimeOffset now, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); return Task.FromResult(Set(userId, gameId, value, now)); }
    public Task<IReadOnlyDictionary<int, double?>> AveragesAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); lock(gate) return Task.FromResult<IReadOnlyDictionary<int,double?>>(ratings.Values.Where(item => ids.Contains(item.GameId)).GroupBy(item => item.GameId).ToDictionary(group => group.Key, group => (double?)group.Average(item => item.Value.Value)));
    }
}