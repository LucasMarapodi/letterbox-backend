using Tcc.Domain.Opinions;
using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Application.Social;

namespace Tcc.Application.Opinions;
public sealed record CommentWithLikes(Comment Comment, int LikeCount);
public sealed record CommentActivitySummary(int ReviewId, int Count, DateTimeOffset Last);
public interface IComments
{
    Task<Comment> AddAsync(Comment comment, CancellationToken ct = default, Func<Comment, CancellationToken, Task>? publish = null);
    Task<Comment?> FindAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CommentWithLikes>> ForReviewAsync(int reviewId, CancellationToken ct = default);
    Task<IReadOnlyList<CommentActivitySummary>> ActivityAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
public sealed class PublishComment(IComments comments, GetReview reviews, OpinionViews views,
    ICurrentSession current, SessionAccess access, TimeProvider clock, PublicationNotifications notifications)
{
    public async Task<CommentDetails> Execute(int reviewId, string? text, CancellationToken cancellationToken = default, int? parentCommentId = null)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        if (reviewId <= 0) throw new Tcc.Domain.Identity.DomainValidationException("ID de review inválido.");
        var draft = Comment.Create(0, userId, reviewId, text, clock.GetUtcNow(), parentCommentId);
        var review = await reviews.FindAsync(reviewId, cancellationToken);
        if (parentCommentId is int parentId)
        {
            var parent = await comments.FindAsync(parentId, cancellationToken) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
            draft = Comment.Reply(0, userId, reviewId, draft.Text, draft.CreatedAt, parent);
        }
        var author = await views.Author(userId, cancellationToken);
        await access.Require(current, cancellationToken);
        var saved = await comments.AddAsync(draft, cancellationToken,
            (comment, ct) => notifications.ForCommentAsync(comment, review.UserId, ct));
        return new(saved, author);
    }
}
public sealed record CommentDetails(Comment Comment, PublicAuthor Author, int LikeCount = 0);
public sealed class QueryComments(IComments comments, GetReview reviews, OpinionViews views)
{
    public async Task<IReadOnlyList<CommentDetails>> Execute(int reviewId, CancellationToken cancellationToken = default)
    {
        await reviews.FindAsync(reviewId, cancellationToken);
        var snapshot = await comments.ForReviewAsync(reviewId, cancellationToken);
        var authors = await views.Authors(snapshot.Select(item => item.Comment.UserId), cancellationToken);
        return snapshot.Select(item => new CommentDetails(item.Comment, authors[item.Comment.UserId], item.LikeCount)).ToArray();
    }
}
