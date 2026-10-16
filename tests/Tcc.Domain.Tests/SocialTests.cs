using Tcc.Domain.Identity;
using Tcc.Domain.Social;

namespace Tcc.Domain.Tests;

public sealed class SocialTests
{
    [Fact]
    public void Following_self_is_rejected_in_domain() =>
        Assert.Throws<DomainValidationException>(() => Follow.Create(1, 1, DateTimeOffset.UtcNow));

    [Theory]
    [InlineData("other")]
    [InlineData("Game")]
    public void Invalid_favorite_kind_is_rejected(string kind) =>
        Assert.Throws<DomainValidationException>(() => Favorite.ParseKind(kind));

    [Fact]
    public void Notification_factories_require_correct_event_and_disallow_self_notification()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<DomainValidationException>(() => Notification.NewReview(1, 1, 1, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => Notification.ReviewComment(1, 2, 1, 0, now));
        Assert.Null(Notification.NewReview(1, 2, 1, now).CommentId);
        Assert.Equal(1, Notification.ReviewComment(1, 2, 1, 1, now).CommentId);
    }
}
