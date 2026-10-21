using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController, Route("api/v1/community/reviews")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CommunityReviewLikesController(MostLikedReviews ranking) : ControllerBase
{
    [HttpGet("most-liked")]
    [ProducesResponseType<LikedReviewPageResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(503)]
    public async Task<ActionResult<LikedReviewPageResponse>> MostLiked([FromQuery] ReviewRankingQuery query, CancellationToken ct)
    {
        var page = await ranking.Execute(query.Period, query.Page, query.PageSize, ct);
        return Ok(new LikedReviewPageResponse(page.Items.Select(item => new LikedReviewResponse(
            ReviewResponse.From(item.Review), item.PeriodLikeCount, item.LastLikeAt)).ToArray(), page.Page, page.PageSize, page.HasNextPage));
    }
}
