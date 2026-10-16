using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Queries;
public sealed class FollowingFeed(IFollows follows, IReviews reviews, ReviewProjection projection,
    ICurrentSession current, SessionAccess access)
{
    public Task<IReadOnlyList<Review>> SnapshotFor(int userId, CancellationToken ct = default)
    {
        var followed = follows.Following(userId);
        return reviews.QueryAsync(new(Authors: followed), ct);
    }
    public async Task<IReadOnlyList<Review>> Snapshot(CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        return await SnapshotFor(userId, cancellationToken);
    }
    public async Task<IReadOnlyList<ReviewDetails>> Execute(CancellationToken cancellationToken = default) =>
        await projection.ExecuteAsync(await Snapshot(cancellationToken), cancellationToken);
}
