namespace Tcc.Domain.Opinions;

public sealed record ReviewLike
{
    public int UserId { get; }
    public int ReviewId { get; }
    public DateTimeOffset CreatedAt { get; }
    public ReviewLike(int userId, int reviewId, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reviewId);
        UserId = userId; ReviewId = reviewId; CreatedAt = createdAt.ToUniversalTime();
    }
}
