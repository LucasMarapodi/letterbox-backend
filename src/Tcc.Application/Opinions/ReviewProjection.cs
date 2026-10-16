using Tcc.Domain.Opinions;

namespace Tcc.Application.Opinions;

public sealed class ReviewProjection(OpinionViews views)
{
    public IReadOnlyList<ReviewDetails> Execute(IEnumerable<Review> reviews) => reviews
        .OrderByDescending(review => review.CreatedAt).ThenBy(review => review.Id, StringComparer.Ordinal)
        .Select(views.Review).ToArray();
}
