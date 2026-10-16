using Tcc.Application.Opinions;

namespace Tcc.Api.Contracts;

public sealed record CommentResponse(int Id, int UserId, int ReviewId, string Text,
    DateTimeOffset CreatedAt, AuthorResponse Author, int? ParentCommentId, int LikeCount)
{
    public static CommentResponse From(CommentDetails details) => new(details.Comment.Id, details.Comment.UserId,
        details.Comment.ReviewId, details.Comment.Text, details.Comment.CreatedAt,
        new(details.Author.Id, details.Author.DisplayName, details.Author.AvatarUrl), details.Comment.ParentCommentId, details.LikeCount);
}

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record CommentRequest([System.ComponentModel.DataAnnotations.Required] string? Text, int? ParentCommentId = null);
public sealed record CommentLikeStatusResponse(bool LikedByMe);
