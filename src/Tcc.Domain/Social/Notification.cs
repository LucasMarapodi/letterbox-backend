namespace Tcc.Domain.Social;

public enum NotificationType { NewReview, ReviewComment }
public sealed record Notification(string Id, string UserId, string ActorId, string ReviewId,
    DateTimeOffset CreatedAt, NotificationType Type, string? CommentId);
