using Tcc.Domain.Opinions;

namespace Tcc.Domain.Tests;

public sealed class ReviewTitleTests
{
    [Fact]
    public void Textual_review_requires_a_title_in_the_domain()
    {
        var error = Assert.ThrowsAny<Exception>(() => Review.CreateRated(1, 1, "Description", 0, DateTimeOffset.UtcNow));
        Assert.Equal("ReviewTitleException", error.GetType().Name);
    }

    [Fact]
    public void Adding_description_to_a_rating_only_review_requires_a_title()
    {
        var review = Review.CreateRated(1, 1, null, 0, DateTimeOffset.UtcNow);
        var error = Assert.ThrowsAny<Exception>(() => review.EditRated(1, true, "Description", null, DateTimeOffset.UtcNow));
        Assert.Equal("ReviewTitleException", error.GetType().Name);
    }
}
