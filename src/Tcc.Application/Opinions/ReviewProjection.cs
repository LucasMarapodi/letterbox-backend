using Tcc.Domain.Opinions;

namespace Tcc.Application.Opinions;

public sealed class ReviewProjection(OpinionViews views, Tcc.Application.Catalog.QueryGames games)
{
    public async Task<IReadOnlyList<ReviewDetails>> ExecuteAsync(IEnumerable<Review> reviews, CancellationToken cancellationToken = default)
    {
        var snapshot = reviews.OrderByDescending(review => review.CreatedAt).ThenBy(review => review.Id, StringComparer.Ordinal).ToArray();
        var found = await games.ResolveAsync(snapshot.Select(review => review.GameId), cancellationToken);
        return snapshot.Select(review => views.Review(review, found[review.GameId])).ToArray();
    }
    public IReadOnlyList<ReviewDetails> Execute(IEnumerable<Review> reviews) => reviews
        .OrderByDescending(review => review.CreatedAt).ThenBy(review => review.Id, StringComparer.Ordinal)
        .Select(views.Review).ToArray();
}
