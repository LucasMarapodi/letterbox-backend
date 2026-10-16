using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Queries;
public sealed class FollowingFeed(IFollows follows, IReviews reviews, ReviewProjection projection,
    ICurrentSession current, SessionAccess access, IPublicationScope publications)
{
    public IReadOnlyList<Review> Snapshot() => publications.Execute(() =>
    {
        var followed = follows.Following(access.Require(current).UserId).ToHashSet(StringComparer.Ordinal);
        return (IReadOnlyList<Review>)reviews.All().Where(review => followed.Contains(review.UserId)).ToArray();
    });
    public Task<IReadOnlyList<ReviewDetails>> Execute(CancellationToken cancellationToken = default) => projection.ExecuteAsync(Snapshot(), cancellationToken);
}
