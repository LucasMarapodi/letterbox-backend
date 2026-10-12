using System.ComponentModel.DataAnnotations;

namespace Tcc.Api.Contracts;

public sealed class RankingQuery
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Period { get; set; }
}
public sealed record RankedReviewResponse(ReviewResponse Review, int CommentCount, DateTimeOffset LastCommentAt);
