using Tcc.Domain.Opinions;

namespace Tcc.Application.Opinions;

public static class ReviewOrdering
{
    public static bool HasTitle(Review review) => !string.IsNullOrEmpty(review.Title);

    public static IOrderedEnumerable<Review> Common(IEnumerable<Review> reviews) => reviews
        .OrderByDescending(HasTitle).ThenByDescending(review => review.CreatedAt).ThenBy(review => review.Id);

    public static IOrderedQueryable<Review> Common(IQueryable<Review> reviews) => reviews
        .OrderByDescending(review => review.Title != null && review.Title != "")
        .ThenByDescending(review => review.CreatedAt).ThenBy(review => review.Id);
}
