using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Tcc.Application.Opinions;

namespace Tcc.Api.Contracts;

public sealed class CommentRankingQuery
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [FromQuery(Name = "period")]
    public string? Period { get; set; }
    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;
    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;
}

public sealed record RankedCommentResponse(int CommentId, int ReviewId, int GameId, int? ParentCommentId,
    string Text, DateTimeOffset CreatedAt, AuthorResponse Author, int LikeCount, int PeriodLikeCount, DateTimeOffset LastLikeAt)
{
    public static RankedCommentResponse From(RankedCommentDetails item) => new(item.CommentId, item.ReviewId, item.GameId,
        item.ParentCommentId, item.Text, item.CreatedAt, new(item.Author.Id, item.Author.DisplayName, item.Author.AvatarUrl),
        item.LikeCount, item.PeriodLikeCount, item.LastLikeAt);
}

public sealed record RankedCommentPageResponse(IReadOnlyList<RankedCommentResponse> Items, int Page, int PageSize, bool HasNextPage);
