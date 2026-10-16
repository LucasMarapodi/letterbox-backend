namespace Tcc.Domain.Opinions;

public sealed record CommentLike
{
    public int UserId { get; }
    public int CommentId { get; }
    public DateTimeOffset CreatedAt { get; }
    public CommentLike(int userId, int commentId, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(commentId);
        UserId = userId; CommentId = commentId; CreatedAt = createdAt.ToUniversalTime();
    }
}
