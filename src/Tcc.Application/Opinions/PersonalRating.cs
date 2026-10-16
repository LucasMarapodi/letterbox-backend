using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Opinions;

public interface IRatings
{
    Rating? Find(string userId, string gameId);
    Rating Set(string userId, string gameId, RatingValue value);
    IReadOnlyList<Rating> ForGame(string gameId);
}

public sealed class PersonalRating(IRatings ratings, QueryGames games, ICurrentSession current, SessionAccess access)
{
    public Rating? Get(string gameId)
    {
        var userId = access.Require(current).UserId;
        games.Get(gameId);
        return ratings.Find(userId, gameId);
    }
    public Rating Set(string gameId, double value)
    {
        var userId = access.Require(current).UserId;
        games.Get(gameId);
        return ratings.Set(userId, gameId, RatingValue.Create(value));
    }
}

public sealed class LocalRatingAverage(IRatings ratings)
{
    public double? ForGame(string gameId)
    {
        var values = ratings.ForGame(gameId);
        return values.Count == 0 ? null : values.Average(rating => rating.Value.Value);
    }
}
