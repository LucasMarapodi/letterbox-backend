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
        var authors = await views.Authors(snapshot.Select(review => review.UserId), cancellationToken);
        return snapshot.OrderByDescending(review => review.CreatedAt).ThenBy(review => review.Id, StringComparer.Ordinal)
            .Select(review => views.Review(review, game, authors[review.UserId])).ToArray();
    }
    public async Task<IReadOnlyList<ReviewDetails>> ForUser(int userId, CancellationToken cancellationToken = default)
    {
        if (await accounts.FindByIdAsync(userId, cancellationToken) is null) throw new ResourceFailure(ResourceFailureKind.NotFound);
        return await projection.ExecuteAsync(reviews.All().Where(review => review.UserId == userId), cancellationToken);
    }
    public async Task<ReviewDetails?> Mine(string gameId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var review = reviews.All().SingleOrDefault(review => review.UserId == userId && review.GameId == gameId);
        var game = await games.GetAsync(gameId, cancellationToken);
        return review is null ? null : views.Review(review, game, await views.Author(review.UserId, cancellationToken));
    }
    public async Task<GameOpinions> Opinions(string gameId, CancellationToken cancellationToken = default)
    {
        var average = averages.ForGame(gameId);
        return new(average, await ForGame(gameId, cancellationToken));
    }
}
