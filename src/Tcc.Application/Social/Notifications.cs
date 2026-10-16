using Tcc.Domain.Social;
using Tcc.Domain.Opinions;
using Tcc.Application.Identity;
using Tcc.Application.Opinions;

namespace Tcc.Application.Social;

public interface INotifications
{
    Task AddRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ForUserAsync(int userId, CancellationToken ct = default);
}

public sealed class PublicationNotifications(IFollows follows, INotifications notifications)
{
    public async Task ForReviewAsync(Review review, CancellationToken ct = default)
    {
        var recipients = await follows.FollowersAsync(review.UserId, ct);
        await notifications.AddRangeAsync(recipients.Select(userId => Notification.NewReview(userId, review.UserId, review.Id, review.CreatedAt)).ToArray(), ct);
    }
    public Task ForCommentAsync(Comment comment, int reviewAuthor, CancellationToken ct = default) =>
        notifications.AddRangeAsync(comment.UserId == reviewAuthor ? [] : [Notification.ReviewComment(reviewAuthor, comment.UserId,
            comment.ReviewId, comment.Id, comment.CreatedAt)], ct);
}

public sealed record NotificationDetails(Notification Notification, PublicAuthor Actor);
public sealed class QueryNotifications(INotifications notifications, ICurrentSession current, SessionAccess access, OpinionViews views)
{
    public async Task<IReadOnlyList<NotificationDetails>> Execute(CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var snapshot = await notifications.ForUserAsync(userId, cancellationToken);
        var authors = await views.Authors(snapshot.Select(item => item.ActorId), cancellationToken);
        return snapshot.Select(item => new NotificationDetails(item, authors[item.ActorId])).ToArray();
    }
}
