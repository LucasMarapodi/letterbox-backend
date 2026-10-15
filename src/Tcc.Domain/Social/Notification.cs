using Tcc.Domain.Identity;

namespace Tcc.Domain.Social;

public enum NotificationType { NewReview, ReviewComment }
public sealed record Notification
{
    public int Id { get; init; }
    public int UserId { get; }
    public int ActorId { get; }
    public int ReviewId { get; }
    public DateTimeOffset CreatedAt { get; }
    public NotificationType Type { get; }
    public int? CommentId { get; }
    private Notification(int id, int userId, int actorId, int reviewId, DateTimeOffset createdAt, NotificationType type, int? commentId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(actorId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reviewId);
        if (userId == actorId) throw new DomainValidationException("Não criar auto-notificação.");
        if (!Enum.IsDefined(type) || type == NotificationType.NewReview && commentId is not null || type == NotificationType.ReviewComment && commentId is null)
            throw new DomainValidationException("Evento de notificação inválido.");
        Id = id; UserId = userId; ActorId = actorId; ReviewId = reviewId;
        CreatedAt = createdAt.ToUniversalTime(); Type = type; CommentId = commentId;
    }
    public static Notification NewReview(int userId, int actorId, int reviewId, DateTimeOffset now) =>
        new(0, userId, actorId, reviewId, now, NotificationType.NewReview, null);
    public static Notification ReviewComment(int userId, int actorId, int reviewId, int commentId, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(commentId);
        return new(0, userId, actorId, reviewId, now, NotificationType.ReviewComment, commentId);
    }
}
