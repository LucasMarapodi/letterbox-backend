using Tcc.Domain.Social;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Social;

public interface INotifications
{
    void AddRange(IReadOnlyList<Notification> notifications);
    IReadOnlyList<Notification> ForUser(string userId);
}

public interface IPublicationScope
{
    T Execute<T>(Func<T> action);
}

public sealed class PublicationNotifications(IFollows follows, INotifications notifications)
{
    public IReadOnlyList<Notification> ForReview(Review review) => follows.Followers(review.UserId)
        .Select(userId => Notification.NewReview(userId, review.UserId, review.Id, review.CreatedAt)).ToArray();
    public IReadOnlyList<Notification> ForComment(Comment comment, string reviewAuthor) =>
        comment.UserId == reviewAuthor ? [] : [Notification.ReviewComment(reviewAuthor, comment.UserId,
            comment.ReviewId, comment.Id, comment.CreatedAt)];
    public void Send(IReadOnlyList<Notification> events) => notifications.AddRange(events);
}
