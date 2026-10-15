using Tcc.Domain.Social;
using Tcc.Domain.Opinions;
using Tcc.Application.Identity;
using Tcc.Application.Opinions;

namespace Tcc.Application.Social;

public interface INotifications
{
    void AddRange(IReadOnlyList<Notification> notifications);
    IReadOnlyList<Notification> ForUser(int userId);
}

public interface IPublicationScope
{
    T Execute<T>(Func<T> action);
    IPreparedPublication Prepare(Action action);
}

public interface IPreparedPublication : IDisposable { void Commit(); }
public sealed class PublicationNotifications(IFollows follows, INotifications notifications)
{
    public Task<IReadOnlyList<int>> ReviewRecipients(int userId, CancellationToken ct = default) => follows.FollowersAsync(userId, ct);
    public IReadOnlyList<Notification> ForReview(Review review, IReadOnlyList<int> recipients) => recipients
        .Select(userId => Notification.NewReview(userId, review.UserId, review.Id, review.CreatedAt)).ToArray();
    public IReadOnlyList<Notification> ForComment(Comment comment, int reviewAuthor) =>
        comment.UserId == reviewAuthor ? [] : [Notification.ReviewComment(reviewAuthor, comment.UserId,
            comment.ReviewId, comment.Id, comment.CreatedAt)];
    public void Send(IReadOnlyList<Notification> events) => notifications.AddRange(events);
}

public sealed record NotificationDetails(Notification Notification, PublicAuthor Actor);
public sealed class QueryNotifications(INotifications notifications, ICurrentSession current, SessionAccess access, OpinionViews views)
{
    public async Task<IReadOnlyList<NotificationDetails>> Execute(CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var snapshot = notifications.ForUser(userId).OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var authors = await views.Authors(snapshot.Select(item => item.ActorId), cancellationToken);
        return snapshot.Select(item => new NotificationDetails(item, authors[item.ActorId])).ToArray();
    }
}
