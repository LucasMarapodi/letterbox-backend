using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Opinions;

public sealed record GameOpinions(double? AverageRating, IReadOnlyList<ReviewDetails> Reviews);

public sealed class QueryReviews(IReviews reviews, QueryGames games, IUserAccounts accounts, OpinionViews views,
    ICurrentSession current, SessionAccess access, LocalRatingAverage averages)
{
    private IReadOnlyList<ReviewDetails> Project(IEnumerable<Review> items) => items
        .OrderByDescending(review => review.CreatedAt).ThenBy(review => review.Id, StringComparer.Ordinal)
        .Select(views.Review).ToArray();

    public IReadOnlyList<ReviewDetails> ForGame(string gameId)
    {
        games.Get(gameId);
        return Project(reviews.All().Where(review => review.GameId == gameId));
    }
    public IReadOnlyList<ReviewDetails> ForUser(string userId)
    {
        if (accounts.FindById(userId) is null) throw new ResourceFailure(ResourceFailureKind.NotFound);
        return Project(reviews.All().Where(review => review.UserId == userId));
    }
    public ReviewDetails? Mine(string gameId)
    {
        var userId = access.Require(current).UserId;
        games.Get(gameId);
        var review = reviews.All().SingleOrDefault(review => review.UserId == userId && review.GameId == gameId);
        return review is null ? null : views.Review(review);
    }
    public GameOpinions Opinions(string gameId) => new(averages.ForGame(gameId), ForGame(gameId));
}
