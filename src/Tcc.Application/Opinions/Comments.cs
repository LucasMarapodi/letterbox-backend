using Tcc.Domain.Opinions;
using Tcc.Application.Identity;

namespace Tcc.Application.Opinions;

public interface IComments
{
    IReadOnlyList<Comment> ForReview(string reviewId);
    void Add(Comment comment);
}

public sealed class PublishComment(IComments comments, GetReview reviews, OpinionViews views,
    ICurrentSession current, SessionAccess access, TimeProvider clock)
{
    public CommentDetails Execute(string reviewId, string? text)
    {
        var userId = access.Require(current).UserId;
        reviews.Execute(reviewId);
        var comment = Comment.Create(Guid.NewGuid().ToString("N"), userId, reviewId, text, clock.GetUtcNow());
        comments.Add(comment);
        return new(comment, views.Author(userId));
    }
}
public sealed record CommentDetails(Comment Comment, PublicAuthor Author);

public sealed class QueryComments(IComments comments, GetReview reviews, OpinionViews views)
{
    public IReadOnlyList<CommentDetails> Execute(string reviewId)
    {
        reviews.Execute(reviewId);
        return comments.ForReview(reviewId).OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id, StringComparer.Ordinal)
            .Select(comment => new CommentDetails(comment, views.Author(comment.UserId))).ToArray();
    }
}
