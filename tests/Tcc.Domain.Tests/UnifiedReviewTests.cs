using Tcc.Domain.Opinions;

namespace Tcc.Domain.Tests;

public sealed class UnifiedReviewTests
{
    [Fact]
    public void New_review_requires_a_rating_and_legacy_edit_must_resolve_it()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<Tcc.Domain.Identity.DomainValidationException>(() => Review.CreateRated(1, 1, null, null, now));
        var rated = Review.CreateRated(1, 1, null, 0, now);
        Assert.Equal(0, rated.Evaluation!.Value.Value);
        var legacy = Review.Create(1, 1, 1, "Old", now, "Fixture review");
        Assert.Throws<Tcc.Domain.Identity.DomainValidationException>(() => legacy.EditRated(1, false, null, null, now));
        Assert.Equal("Old", legacy.EditRated(1, false, null, 3.25, now).Text);
        Assert.Equal("", rated.EditRated(1, true, null, null, now).Text);
        Assert.Throws<Tcc.Domain.Identity.DomainValidationException>(() => rated.EditRated(1, false, null, 6, now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Optional_description_is_normalized_to_empty(string? text) =>
        Assert.Equal("", Review.Create(0, 1, 1, text, DateTimeOffset.UtcNow, "Fixture review").Text);
}
