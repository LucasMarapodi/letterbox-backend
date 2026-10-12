using Tcc.Domain.Identity;

namespace Tcc.Domain.Social;

public enum NotificationType { NewReview, ReviewComment }
public sealed record Notification
{
    public string Id { get; }
    public string UserId { get; }
    public string ActorId { get; }
    public string ReviewId { get; }
    public DateTimeOffset CreatedAt { get; }
    public NotificationType Type { get; }
    public string? CommentId { get; }
    private Notification(string userId, string actorId, string reviewId, DateTimeOffset now, NotificationType type, string? commentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewId);
        if (userId == actorId) throw new DomainValidationException("Não criar auto-notificação.");
        Id = Guid.NewGuid().ToString("N"); UserId = userId; ActorId = actorId; ReviewId = reviewId;
        CreatedAt = now.ToUniversalTime(); Type = type; CommentId = commentId;
    }
    public static Notification NewReview(string userId, string actorId, string reviewId, DateTimeOffset now) =>
        new(userId, actorId, reviewId, now, NotificationType.NewReview, null);
    public static Notification ReviewComment(string userId, string actorId, string reviewId, string commentId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commentId);
        return new(userId, actorId, reviewId, now, NotificationType.ReviewComment, commentId);
    }
}
