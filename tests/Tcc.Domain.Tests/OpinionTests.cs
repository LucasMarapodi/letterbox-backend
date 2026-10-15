using Tcc.Domain.Identity;
using Tcc.Domain.Opinions;

namespace Tcc.Domain.Tests;

public sealed class OpinionTests
{
    [Fact]
    public void Replies_require_a_persisted_root_in_the_same_review()
    {
        var now = DateTimeOffset.UtcNow;
        var root = Comment.Create(1, 1, 1, "Root", now);
        var reply = Comment.Reply(2, 2, 1, "Reply", now, root);
        Assert.Equal(root.Id, reply.ParentCommentId);
        Assert.Throws<DomainValidationException>(() => Comment.Reply(3, 2, 2, "Cross review", now, root));
        Assert.Throws<DomainValidationException>(() => Comment.Reply(3, 2, 1, "Nested", now, reply));
        Assert.Throws<DomainValidationException>(() => Comment.Create(1, 1, 1, "Cycle", now, 1));
        Assert.Throws<DomainValidationException>(() => Comment.Create(0, 1, 1, "Invalid parent", now, 0));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1d)]
    [InlineData(5.01d)]
    public void Invalid_rating_never_enters_domain(double value) =>
        Assert.Throws<DomainValidationException>(() => RatingValue.Create(value));

    [Fact]
    public void Review_ownership_and_text_limits_are_domain_invariants()
    {
        var now = DateTimeOffset.UtcNow;
        var review = Review.Create(1, 1, 1, new string('a', 10000), now);
        Assert.Throws<ReviewOwnershipException>(() => review.Edit(2, "Text", now));
        Assert.Throws<DomainValidationException>(() => review.Edit(1, new string('a', 10001), now));
        Assert.Throws<DomainValidationException>(() => review.Edit(1, " ", now));
        var edited = review.Edit(1, "Edited", now.AddMinutes(1));
        Assert.Equal(review.CreatedAt, edited.CreatedAt);
        Assert.Equal(review.Id, edited.Id);
        Assert.Equal(review.UserId, edited.UserId);
        Assert.Equal(review.GameId, edited.GameId);
        Assert.Null(review.UpdatedAt);
    }

    [Fact]
    public void Comment_text_has_its_own_limit()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(2000, Comment.Create(1, 1, 1, new string('a', 2000), now).Text.Length);
        Assert.Throws<DomainValidationException>(() => Comment.Create(1, 1, 1, new string('a', 2001), now));
        Assert.Throws<DomainValidationException>(() => Comment.Create(1, 1, 1, " ", now));
    }
}
