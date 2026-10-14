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

public sealed class PersonalRating(IRatings ratings, QueryGames games, ICurrentSession current, SessionAccess access,
    Tcc.Application.Social.IPublicationScope publications)
{
    public async Task<Rating?> Get(string gameId, CancellationToken cancellationToken = default)
    {
        var userId = access.Require(current).UserId;
        await games.GetAsync(gameId, cancellationToken);
        return ratings.Find(userId, gameId);
    }
    public async Task<Rating> Set(string gameId, double value, CancellationToken cancellationToken = default)
    {
        var userId = access.Require(current).UserId;
        var ratingValue = RatingValue.Create(value);
        await games.GetAsync(gameId, cancellationToken);
        return publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            access.Require(current);
            return ratings.Set(userId, gameId, ratingValue);
        });
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
