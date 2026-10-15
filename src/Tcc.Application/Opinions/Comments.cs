using Tcc.Domain.Opinions;
using Tcc.Application.Identity;
using Tcc.Application.Social;

namespace Tcc.Application.Opinions;
public interface IComments
{
    IReadOnlyList<Comment> ForReview(int reviewId);
    void Add(Comment comment);
    IReadOnlyList<Comment> InWindow(DateTimeOffset from, DateTimeOffset to);
}
public sealed class PublishComment(IComments comments, GetReview reviews, OpinionViews views,
    ICurrentSession current, SessionAccess access, TimeProvider clock, PublicationNotifications notifications, IPublicationScope publications)
{
    public async Task<CommentDetails> Execute(int reviewId, string? text, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var draft = Comment.Create(Guid.NewGuid().ToString("N"), userId, reviewId, text, clock.GetUtcNow());
        var author = await views.Author(userId, cancellationToken);
        await access.Require(current, cancellationToken);
        var review = await reviews.FindAsync(reviewId, cancellationToken);
        return publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var comment = Comment.Create(draft.Id, userId, reviewId, draft.Text, clock.GetUtcNow());
            var events = notifications.ForComment(comment, review.UserId);
            comments.Add(comment);
            notifications.Send(events);
            return new CommentDetails(comment, author);
        });
    }
}
public sealed record CommentDetails(Comment Comment, PublicAuthor Author);
public sealed class QueryComments(IComments comments, GetReview reviews, OpinionViews views)
{
    public async Task<IReadOnlyList<CommentDetails>> Execute(int reviewId, CancellationToken cancellationToken = default)
    {
        await reviews.FindAsync(reviewId, cancellationToken);
        var snapshot = comments.ForReview(reviewId).OrderBy(comment => comment.CreatedAt).ThenBy(comment => comment.Id, StringComparer.Ordinal).ToArray();
        var authors = await views.Authors(snapshot.Select(comment => comment.UserId), cancellationToken);
        return snapshot.Select(comment => new CommentDetails(comment, authors[comment.UserId])).ToArray();
    }
}
