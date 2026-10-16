using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Queries;
public sealed class FollowingFeed(IFollows follows, IReviews reviews, ReviewProjection projection,
    ICurrentSession current, SessionAccess access, IPublicationScope publications)
{
    public IReadOnlyList<Review> SnapshotFor(int userId)
    {
        var followed = follows.Following(userId).ToHashSet();
        return reviews.All().Where(review => followed.Contains(review.UserId)).ToArray();
    }
    public async Task<IReadOnlyList<Review>> Snapshot(CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        return publications.Execute(() => SnapshotFor(userId));
    }
    public async Task<IReadOnlyList<ReviewDetails>> Execute(CancellationToken cancellationToken = default) =>
        await projection.ExecuteAsync(await Snapshot(cancellationToken), cancellationToken);
}
