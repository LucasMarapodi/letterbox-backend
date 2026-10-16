using Tcc.Domain.Identity;
using Tcc.Domain.Social;

namespace Tcc.Domain.Tests;

public sealed class SocialTests
{
    [Fact]
    public void Following_self_is_rejected_in_domain() =>
        Assert.Throws<DomainValidationException>(() => Follow.Create("same", "same"));

    [Theory]
    [InlineData("other")]
    [InlineData("Game")]
    public void Invalid_favorite_kind_is_rejected(string kind) =>
        Assert.Throws<DomainValidationException>(() => Favorite.ParseKind(kind));

    [Fact]
    public void Notification_factories_require_correct_event_and_disallow_self_notification()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<DomainValidationException>(() => Notification.NewReview("same", "same", "review", now));
        Assert.Throws<ArgumentException>(() => Notification.ReviewComment("recipient", "actor", "review", "", now));
        Assert.Null(Notification.NewReview("recipient", "actor", "review", now).CommentId);
        Assert.Equal("comment", Notification.ReviewComment("recipient", "actor", "review", "comment", now).CommentId);
    }
}
