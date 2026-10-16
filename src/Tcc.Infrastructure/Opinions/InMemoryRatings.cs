using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;

namespace Tcc.Infrastructure.Opinions;

public sealed class InMemoryRatings : IRatings
{
    private readonly Lock gate = new();
    private readonly Dictionary<(int User, int Game), Rating> ratings = [];
    public Rating? Find(int userId, int gameId)
    {
        lock (gate) return ratings.GetValueOrDefault((userId, gameId));
    }
    public Rating Set(int userId, int gameId, RatingValue value)
    {
        lock (gate)
        {
            var key = (userId, gameId);
            var rating = new Rating(ratings.GetValueOrDefault(key)?.Id ?? Guid.NewGuid().ToString("N"), userId, gameId, value);
            ratings[key] = rating;
            return rating;
        }
    }
    public IReadOnlyList<Rating> ForGame(int gameId)
    {
        lock (gate) return ratings.Values.Where(rating => rating.GameId == gameId).ToArray();
    }
}
