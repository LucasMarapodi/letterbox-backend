using Tcc.Domain.Opinions;

namespace Tcc.Application.Opinions;

public sealed class ReviewProjection(OpinionViews views, Tcc.Application.Catalog.QueryGames games)
{
    public async Task<IReadOnlyList<ReviewDetails>> ExecuteAsync(IEnumerable<Review> reviews, CancellationToken cancellationToken = default)
    {
        var snapshot = reviews.OrderByDescending(review => review.CreatedAt).ThenBy(review => review.Id).ToArray();
        var found = await games.ResolveAsync(snapshot.Select(review => review.GameId), cancellationToken);
        var authors = await views.Authors(snapshot.Select(review => review.UserId), cancellationToken);
        return snapshot.Select(review => views.Review(review, found[review.GameId], authors[review.UserId])).ToArray();
    }

}
