using Tcc.Application.Social;
using Tcc.Application.Opinions;
using Tcc.Domain.Social;
namespace Tcc.Infrastructure.Social;

public sealed class InMemoryFavorites(IReviews reviews) : IFavorites
{
    private readonly Lock gate = new();
    private readonly Dictionary<(int User, FavoriteKind Kind, int Target), Favorite> favorites = [];
    private int next;
    public Task<Favorite> SetAsync(int userId, FavoriteKind kind, int targetId, DateTimeOffset now, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            var key = (userId, kind, targetId);
            if (favorites.TryGetValue(key, out var existing)) return Task.FromResult(existing);
            var favorite = new Favorite(++next, userId, kind, targetId);
            favorites.Add(key, favorite);
            return Task.FromResult(favorite);
        }
    }
    public Task<Favorite?> FindAsync(int userId, FavoriteKind kind, int targetId, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult(favorites.GetValueOrDefault((userId, kind, targetId))); }
    public Task RemoveAsync(int userId, FavoriteKind kind, int targetId, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (gate) favorites.Remove((userId, kind, targetId)); return Task.CompletedTask; }
    public IReadOnlyList<Favorite> ForUser(int userId) { lock (gate) return favorites.Values.Where(item => item.UserId == userId).ToArray(); }
    public async Task<FavoriteSnapshot> SnapshotAsync(int userId, CancellationToken ct = default)
    {
        var items = ForUser(userId).OrderBy(item => item.TargetId).ToArray();
        var ids = items.Where(item => item.Kind == FavoriteKind.Review).Select(item => item.TargetId).ToArray();
        var found = await reviews.QueryAsync(new(Ids: ids), ct);
        return new(items.Where(item => item.Kind == FavoriteKind.Game).Select(item => item.TargetId).ToArray(), found.OrderBy(item => item.Id).ToArray());
    }
}
