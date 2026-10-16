using Tcc.Domain.Opinions;

namespace Tcc.Domain.Tests;

public sealed class UnifiedReviewTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Optional_description_is_normalized_to_empty(string? text) =>
        Assert.Equal("", Review.Create(0, 1, 1, text, DateTimeOffset.UtcNow).Text);
}
