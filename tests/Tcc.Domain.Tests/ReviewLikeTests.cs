using Tcc.Domain.Opinions;

namespace Tcc.Domain.Tests;

public sealed class ReviewLikeTests
{
    [Fact]
    public void Like_has_positive_integer_keys_and_UTC_creation()
    {
        var instant = new DateTimeOffset(2026,10,16,12,0,0,TimeSpan.FromHours(-3));
        var like = new ReviewLike(1, 2, instant);
        Assert.Equal(1, like.UserId);
        Assert.Equal(2, like.ReviewId);
        Assert.Equal(TimeSpan.Zero, like.CreatedAt.Offset);
        Assert.Equal(instant.ToUniversalTime(), like.CreatedAt);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReviewLike(0, 2, instant));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReviewLike(1, -1, instant));
    }
}
