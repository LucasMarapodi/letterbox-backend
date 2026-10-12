using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Application.Social;

namespace Tcc.Application.Queries;

public sealed class FollowingFeed(IFollows follows, IReviews reviews, ReviewProjection projection,
    ICurrentSession current, SessionAccess access, IPublicationScope publications)
{
    public IReadOnlyList<ReviewDetails> Execute() => publications.Execute(() =>
    {
        var followed = follows.Following(access.Require(current).UserId).ToHashSet(StringComparer.Ordinal);
        return projection.Execute(reviews.All().Where(review => followed.Contains(review.UserId)));
    });
}
