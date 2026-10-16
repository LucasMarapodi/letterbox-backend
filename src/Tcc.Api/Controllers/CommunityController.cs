using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Queries;

namespace Tcc.Api.Controllers;

[ApiController, Route("api/v1/community")]
public sealed class CommunityController(RecentReviews recent, MostCommentedReviews rankedReviews, GamesInDiscussion rankedGames) : ControllerBase
{
    [HttpGet("reviews/recent")]
    public ActionResult<IReadOnlyList<ReviewResponse>> Recent() => Ok(recent.Execute().Select(ReviewResponse.From).ToArray());
    [HttpGet("reviews/most-commented")]
    [ProducesResponseType<IReadOnlyList<RankedReviewResponse>>(200)]
    [ProducesResponseType<ApiError>(400)]
    public ActionResult<IReadOnlyList<RankedReviewResponse>> MostCommented([FromQuery] RankingQuery query) =>
        Ok(rankedReviews.Execute(query.Period).Select(item => new RankedReviewResponse(
            ReviewResponse.From(item.Review), item.CommentCount, item.LastCommentAt)).ToArray());
    [HttpGet("games/in-discussion")]
    [ProducesResponseType<IReadOnlyList<RankedGameResponse>>(200)]
    [ProducesResponseType<ApiError>(400)]
    public ActionResult<IReadOnlyList<RankedGameResponse>> GamesInDiscussion([FromQuery] RankingQuery query) =>
        Ok(rankedGames.Execute(query.Period).Select(item => new RankedGameResponse(
            GameResponse.From(item.Game, item.AverageRating), item.CommentCount, item.LastCommentAt)).ToArray());
}
