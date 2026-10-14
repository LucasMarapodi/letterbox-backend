using Tcc.Domain.Opinions;
using Tcc.Application.Identity;
using Tcc.Application.Social;

namespace Tcc.Application.Opinions;

public interface IComments
{
    IReadOnlyList<Comment> ForReview(string reviewId);
    void Add(Comment comment);
}

public sealed class PublishComment(IComments comments, GetReview reviews, OpinionViews views,
    ICurrentSession current, SessionAccess access, TimeProvider clock, PublicationNotifications notifications, IPublicationScope publications)
{
    public CommentDetails Execute(string reviewId, string? text) => publications.Execute(() =>
    {
        var userId = access.Require(current).UserId;
        var review = reviews.Find(reviewId);
        var comment = Comment.Create(Guid.NewGuid().ToString("N"), userId, reviewId, text, clock.GetUtcNow());
        var result = new CommentDetails(comment, views.Author(userId));
        var events = notifications.ForComment(comment, review.UserId);
        comments.Add(comment);
        notifications.Send(events);
        return result;
    });
}
public sealed record CommentDetails(Comment Comment, PublicAuthor Author);

public sealed class QueryComments(IComments comments, GetReview reviews, OpinionViews views)
{
    public IReadOnlyList<CommentDetails> Execute(string reviewId)
    {
        reviews.Find(reviewId);
        return comments.ForReview(reviewId).OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id, StringComparer.Ordinal)
            .Select(comment => new CommentDetails(comment, views.Author(comment.UserId))).ToArray();
    }
}
