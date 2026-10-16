using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Queries;

namespace Tcc.Api.Controllers;

[ApiController, Route("api/v1/community")]
public sealed class CommunityController(RecentReviews recent, MostCommentedReviews rankedReviews, GamesInDiscussion rankedGames) : ControllerBase
{
    [HttpGet("reviews/recent")]
    public async Task<ActionResult<IReadOnlyList<ReviewResponse>>> Recent(CancellationToken cancellationToken) => Ok((await recent.Execute(cancellationToken)).Select(ReviewResponse.From).ToArray());
    [HttpGet("reviews/most-commented")]
    [ProducesResponseType<IReadOnlyList<RankedReviewResponse>>(200)]
    [ProducesResponseType<ApiError>(400)]
    public async Task<ActionResult<IReadOnlyList<RankedReviewResponse>>> MostCommented([FromQuery] RankingQuery query, CancellationToken cancellationToken) =>
        Ok((await rankedReviews.Execute(query.Period, cancellationToken)).Select(item => new RankedReviewResponse(
            ReviewResponse.From(item.Review), item.CommentCount, item.LastCommentAt)).ToArray());
    [HttpGet("games/in-discussion")]
    [ProducesResponseType<IReadOnlyList<RankedGameResponse>>(200)]
    [ProducesResponseType<ApiError>(400)]
    public async Task<ActionResult<IReadOnlyList<RankedGameResponse>>> GamesInDiscussion([FromQuery] RankingQuery query, CancellationToken cancellationToken) =>
        Ok((await rankedGames.Execute(query.Period, cancellationToken)).Select(item => new RankedGameResponse(
            GameResponse.From(item.Game, item.AverageRating), item.CommentCount, item.LastCommentAt)).ToArray());
}
