using Tcc.Domain.Opinions;

namespace Tcc.Domain.Tests;

public sealed class ReviewLikeTests
{
    [Fact]
    public void Like_has_positive_integer_keys_and_UTC_creation()
    {
        var type = typeof(Review).Assembly.GetType("Tcc.Domain.Opinions.ReviewLike");
        Assert.NotNull(type);
        var instant = new DateTimeOffset(2026,10,16,12,0,0,TimeSpan.FromHours(-3));
        var like = Activator.CreateInstance(type, 1, 2, instant);
        Assert.Equal(1, type.GetProperty("UserId")!.GetValue(like));
        Assert.Equal(2, type.GetProperty("ReviewId")!.GetValue(like));
        Assert.Equal(TimeSpan.Zero, ((DateTimeOffset)type.GetProperty("CreatedAt")!.GetValue(like)!).Offset);
    }
}
