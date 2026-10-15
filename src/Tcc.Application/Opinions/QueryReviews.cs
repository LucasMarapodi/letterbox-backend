using Tcc.Application.Catalog;
using Tcc.Application.Identity;

namespace Tcc.Application.Opinions;

public sealed record GameOpinions(double? AverageRating, IReadOnlyList<ReviewDetails> Reviews);
public sealed class QueryReviews(IReviews reviews, QueryGames games, IUserAccounts accounts, OpinionViews views,
    ICurrentSession current, SessionAccess access, LocalRatingAverage averages, ReviewProjection projection)
{
    public async Task<IReadOnlyList<ReviewDetails>> ForGame(int gameId, CancellationToken cancellationToken = default)
    {
        var snapshot = await reviews.QueryAsync(new(GameId: gameId), cancellationToken);
        var game = await games.GetAsync(gameId, cancellationToken);
        var authors = await views.Authors(snapshot.Select(review => review.UserId), cancellationToken);
        return snapshot.OrderByDescending(review => review.CreatedAt).ThenBy(review => review.Id)
            .Select(review => views.Review(review, game, authors[review.UserId])).ToArray();
    }
    public async Task<IReadOnlyList<ReviewDetails>> ForUser(int userId, CancellationToken cancellationToken = default)
    {
        if (await accounts.FindByIdAsync(userId, cancellationToken) is null) throw new ResourceFailure(ResourceFailureKind.NotFound);
        return await projection.ExecuteAsync(await reviews.QueryAsync(new(UserId: userId), cancellationToken), cancellationToken);
    }
    public async Task<ReviewDetails?> Mine(int gameId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var review = (await reviews.QueryAsync(new(GameId: gameId, UserId: userId), cancellationToken)).SingleOrDefault();
        var game = await games.GetAsync(gameId, cancellationToken);
        return review is null ? null : views.Review(review, game, await views.Author(review.UserId, cancellationToken));
    }
    public async Task<GameOpinions> Opinions(int gameId, CancellationToken cancellationToken = default)
    {
        var average = await averages.ForGame(gameId, cancellationToken);
        return new(average, await ForGame(gameId, cancellationToken));
    }
}
