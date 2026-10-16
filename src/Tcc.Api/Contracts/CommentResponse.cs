using Tcc.Application.Opinions;

namespace Tcc.Api.Contracts;

public sealed record CommentResponse(string Id, int UserId, string ReviewId, string Text,
    DateTimeOffset CreatedAt, AuthorResponse Author)
{
    public static CommentResponse From(CommentDetails details) => new(details.Comment.Id, details.Comment.UserId,
        details.Comment.ReviewId, details.Comment.Text, details.Comment.CreatedAt,
        new(details.Author.Id, details.Author.DisplayName, details.Author.AvatarUrl));
}
