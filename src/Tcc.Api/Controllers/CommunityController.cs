using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Queries;

namespace Tcc.Api.Controllers;

[ApiController, Route("api/v1/community")]
public sealed class CommunityController(RecentReviews recent, MostCommentedReviews rankedReviews) : ControllerBase
{
    [HttpGet("reviews/recent")]
    public ActionResult<IReadOnlyList<ReviewResponse>> Recent() => Ok(recent.Execute().Select(ReviewResponse.From).ToArray());
    [HttpGet("reviews/most-commented")]
    public ActionResult<IReadOnlyList<RankedReviewResponse>> MostCommented([FromQuery] RankingQuery query) =>
        Ok(rankedReviews.Execute(query.Period).Select(item => new RankedReviewResponse(
            ReviewResponse.From(item.Review), item.CommentCount, item.LastCommentAt)).ToArray());
}
