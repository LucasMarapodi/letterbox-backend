using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController, Route("api/v1/community/comments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CommunityCommentsController(MostLikedComments ranking) : ControllerBase
{
    [HttpGet("most-liked")]
    [ProducesResponseType<RankedCommentPageResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    public async Task<ActionResult<RankedCommentPageResponse>> MostLiked([FromQuery] CommentRankingQuery query, CancellationToken cancellationToken)
    {
        var page = await ranking.Execute(query.Period, query.Page, query.PageSize, cancellationToken);
        return Ok(new RankedCommentPageResponse(page.Items.Select(RankedCommentResponse.From).ToArray(), page.Page, page.PageSize, page.HasNextPage));
    }
}
