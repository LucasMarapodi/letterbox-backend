using Tcc.Domain.Identity;

namespace Tcc.Domain.Social;

public enum NotificationType { NewReview, ReviewComment }
public sealed record Notification
{
    public string Id { get; }
    public int UserId { get; }
    public int ActorId { get; }
    public int ReviewId { get; }
    public DateTimeOffset CreatedAt { get; }
    public NotificationType Type { get; }
    public string? CommentId { get; }
    private Notification(int userId, int actorId, int reviewId, DateTimeOffset now, NotificationType type, string? commentId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(actorId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reviewId);
        if (userId == actorId) throw new DomainValidationException("Não criar auto-notificação.");
        Id = Guid.NewGuid().ToString("N"); UserId = userId; ActorId = actorId; ReviewId = reviewId;
        CreatedAt = now.ToUniversalTime(); Type = type; CommentId = commentId;
    }
    public static Notification NewReview(int userId, int actorId, int reviewId, DateTimeOffset now) =>
        new(userId, actorId, reviewId, now, NotificationType.NewReview, null);
    public static Notification ReviewComment(int userId, int actorId, int reviewId, string commentId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commentId);
        return new(userId, actorId, reviewId, now, NotificationType.ReviewComment, commentId);
    }
}
