using Tcc.Application.Catalog;
using Tcc.Application.Identity;

namespace Tcc.Application.Opinions;

public sealed record GameOpinions(double? AverageRating, IReadOnlyList<ReviewDetails> Reviews);
public sealed class QueryReviews(IReviews reviews, QueryGames games, IUserAccounts accounts, OpinionViews views,
    ICurrentSession current, SessionAccess access, LocalRatingAverage averages, ReviewProjection projection)
{
    public async Task<IReadOnlyList<ReviewDetails>> ForGame(string gameId, CancellationToken cancellationToken = default)
    {
        var snapshot = reviews.All().Where(review => review.GameId == gameId).ToArray();
        var game = await games.GetAsync(gameId, cancellationToken);
        return snapshot.OrderByDescending(review => review.CreatedAt).ThenBy(review => review.Id, StringComparer.Ordinal)
            .Select(review => views.Review(review, game)).ToArray();
    }
    public Task<IReadOnlyList<ReviewDetails>> ForUser(string userId, CancellationToken cancellationToken = default)
    {
        if (accounts.FindById(userId) is null) throw new ResourceFailure(ResourceFailureKind.NotFound);
        return projection.ExecuteAsync(reviews.All().Where(review => review.UserId == userId), cancellationToken);
    }
    public async Task<ReviewDetails?> Mine(string gameId, CancellationToken cancellationToken = default)
    {
        var userId = access.Require(current).UserId;
        var review = reviews.All().SingleOrDefault(review => review.UserId == userId && review.GameId == gameId);
        var game = await games.GetAsync(gameId, cancellationToken);
        return review is null ? null : views.Review(review, game);
    }
    public async Task<GameOpinions> Opinions(string gameId, CancellationToken cancellationToken = default)
    {
        var average = averages.ForGame(gameId);
        return new(average, await ForGame(gameId, cancellationToken));
    }
}
