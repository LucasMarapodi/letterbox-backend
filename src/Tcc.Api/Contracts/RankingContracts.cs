using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Tcc.Api.Contracts;

public sealed class RankingQuery
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [FromQuery(Name = "period")]
    public string? Period { get; set; }
}
public sealed record RankedReviewResponse(ReviewResponse Review, int CommentCount, DateTimeOffset LastCommentAt);
public sealed record RankedGameResponse(GameResponse Game, int CommentCount, DateTimeOffset LastCommentAt);
