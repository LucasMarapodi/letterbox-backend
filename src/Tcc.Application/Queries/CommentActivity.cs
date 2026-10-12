using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Queries;

public sealed record ReviewActivity(Review Review, int CommentCount, DateTimeOffset LastCommentAt);

public sealed class CommentActivity(IReviews reviews, IComments comments, TimeProvider clock, IPublicationScope publications)
{
    public IReadOnlyList<ReviewActivity> Execute(string? period) => publications.Execute(() =>
    {
        var window = RankingPeriod.Parse(period);
        var now = clock.GetUtcNow();
        var result = new List<ReviewActivity>();
        foreach (var review in reviews.All())
        {
            var eligible = comments.ForReview(review.Id).Where(comment => window.Contains(comment.CreatedAt, now)).ToArray();
            if (eligible.Length > 0) result.Add(new(review, eligible.Length, eligible.Max(comment => comment.CreatedAt)));
        }
        return (IReadOnlyList<ReviewActivity>)result;
    });
}

internal static class ActivityOrdering
{
    internal static IEnumerable<T> Sort<T>(IEnumerable<T> items, Func<T, int> count,
        Func<T, DateTimeOffset> last, Func<T, string> id) => items.OrderByDescending(count)
        .ThenByDescending(last).ThenBy(id, StringComparer.Ordinal);
}

public sealed record RankedReviewDetails(ReviewDetails Review, int CommentCount, DateTimeOffset LastCommentAt);
public sealed class MostCommentedReviews(CommentActivity activity, OpinionViews views)
{
    public IReadOnlyList<RankedReviewDetails> Execute(string? period) =>
        ActivityOrdering.Sort(activity.Execute(period), item => item.CommentCount, item => item.LastCommentAt, item => item.Review.Id)
            .Select(item => new RankedReviewDetails(views.Review(item.Review), item.CommentCount, item.LastCommentAt)).ToArray();
}
