using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Opinions;

public interface IRatings
{
    Rating? Find(int userId, int gameId);
    Rating Set(int userId, int gameId, RatingValue value);
    IReadOnlyList<Rating> ForGame(int gameId);
}

public sealed class PersonalRating(IRatings ratings, QueryGames games, ICurrentSession current, SessionAccess access,
    Tcc.Application.Social.IPublicationScope publications)
{
    public async Task<Rating?> Get(int gameId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        await games.GetAsync(gameId, cancellationToken);
        return ratings.Find(userId, gameId);
    }
    public async Task<Rating> Set(int gameId, double value, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var ratingValue = RatingValue.Create(value);
        await games.GetAsync(gameId, cancellationToken);
        await access.Require(current, cancellationToken);
        return publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ratings.Set(userId, gameId, ratingValue);
        });
    }
}

public sealed class LocalRatingAverage(IRatings ratings)
{
    public double? ForGame(int gameId)
    {
        var values = ratings.ForGame(gameId);
        return values.Count == 0 ? null : values.Average(rating => rating.Value.Value);
    }
}
