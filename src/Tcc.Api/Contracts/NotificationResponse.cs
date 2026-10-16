using System.Text.Json.Serialization;
using Tcc.Application.Social;
using Tcc.Domain.Social;

namespace Tcc.Api.Contracts;

public sealed record NotificationResponse(string Id, int UserId, int ActorId, string ReviewId,
    DateTimeOffset CreatedAt, string Type, AuthorResponse Actor,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CommentId)
{
    public static NotificationResponse From(NotificationDetails details)
    {
        var item = details.Notification;
        return new(item.Id, item.UserId, item.ActorId, item.ReviewId, item.CreatedAt,
            item.Type == NotificationType.NewReview ? "new-review" : "review-comment",
            new(details.Actor.Id, details.Actor.DisplayName, details.Actor.AvatarUrl), item.CommentId);
    }
}
