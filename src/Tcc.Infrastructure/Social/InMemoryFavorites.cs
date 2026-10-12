using Tcc.Application.Social;
using Tcc.Domain.Social;

namespace Tcc.Infrastructure.Social;

public sealed class InMemoryFavorites : IFavorites
{
    private readonly Lock gate = new();
    private readonly Dictionary<(string User, FavoriteKind Kind, string Target), Favorite> favorites = [];
    public Favorite Set(string userId, FavoriteKind kind, string targetId)
    {
        lock (gate)
        {
            var key = (userId, kind, targetId);
            if (favorites.TryGetValue(key, out var existing)) return existing;
            var favorite = new Favorite(Guid.NewGuid().ToString("N"), userId, kind, targetId);
            favorites.Add(key, favorite);
            return favorite;
        }
    }
    public Favorite? Find(string userId, FavoriteKind kind, string targetId) { lock (gate) return favorites.GetValueOrDefault((userId, kind, targetId)); }
    public void Remove(string userId, FavoriteKind kind, string targetId) { lock (gate) favorites.Remove((userId, kind, targetId)); }
    public IReadOnlyList<Favorite> ForUser(string userId) { lock (gate) return favorites.Values.Where(item => item.UserId == userId).ToArray(); }
}
