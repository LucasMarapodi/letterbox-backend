using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Queries;

public sealed record ReviewActivity(Review Review, int CommentCount, DateTimeOffset LastCommentAt);

public sealed class CommentActivity(IReviews reviews, IComments comments, TimeProvider clock)
{
    public async Task<IReadOnlyList<ReviewActivity>> Execute(string? period, CancellationToken ct = default)
    {
        var window = RankingPeriod.Parse(period);
        var now = clock.GetUtcNow();
        var grouped = (await comments.ActivityAsync(now - window.Duration, now, ct)).ToDictionary(item => item.ReviewId);
        var result = new List<ReviewActivity>();
        foreach (var chunk in grouped.Keys.Chunk(500))
            foreach (var review in await reviews.QueryAsync(new(Ids: chunk), ct))
                result.Add(new(review, grouped[review.Id].Count, grouped[review.Id].Last));
        return result;
    }
}

internal static class ActivityOrdering
{
    internal static IEnumerable<T> Sort<T, TKey>(IEnumerable<T> items, Func<T, int> count,
        Func<T, DateTimeOffset> last, Func<T, TKey> id) => items.OrderByDescending(count)
        .ThenByDescending(last).ThenBy(id);
}

public sealed record RankedReviewDetails(ReviewDetails Review, int CommentCount, DateTimeOffset LastCommentAt);
public sealed class MostCommentedReviews(CommentActivity activity, OpinionViews views, Tcc.Application.Catalog.QueryGames games)
{
    public async Task<IReadOnlyList<RankedReviewDetails>> Execute(string? period, CancellationToken cancellationToken = default)
    {
        var snapshot = ActivityOrdering.Sort(await activity.Execute(period, cancellationToken), item => item.CommentCount, item => item.LastCommentAt, item => item.Review.Id).ToArray();
        var found = await games.ResolveAsync(snapshot.Select(item => item.Review.GameId), cancellationToken);
        var authors = await views.Authors(snapshot.Select(item => item.Review.UserId), cancellationToken);
        var counts = await views.LikeCounts(snapshot.Select(item => item.Review), cancellationToken);
        return snapshot.Select(item => new RankedReviewDetails(views.Review(item.Review, found[item.Review.GameId], authors[item.Review.UserId], counts.GetValueOrDefault(item.Review.Id)), item.CommentCount, item.LastCommentAt)).ToArray();
    }
}
