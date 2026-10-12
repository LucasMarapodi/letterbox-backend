using Tcc.Domain.Opinions;

namespace Tcc.Application.Opinions;

public interface IComments
{
    IReadOnlyList<Comment> ForReview(string reviewId);
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
