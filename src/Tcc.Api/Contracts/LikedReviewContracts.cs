using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Tcc.Api.Contracts;

public sealed class ReviewRankingQuery
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [FromQuery(Name = "period")]
    public string? Period { get; set; }
    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;
    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;
}

public sealed record LikedReviewResponse(ReviewResponse Review, int PeriodLikeCount, DateTimeOffset LastLikeAt);
public sealed record LikedReviewPageResponse(IReadOnlyList<LikedReviewResponse> Items, int Page, int PageSize, bool HasNextPage);
