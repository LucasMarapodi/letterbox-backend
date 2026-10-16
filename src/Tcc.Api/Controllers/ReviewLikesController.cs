using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Api.Filters;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController, Authorize, Route("api/v1/reviews/{reviewId}/like")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReviewLikesController(PersonalReviewLike likes) : ControllerBase
{
    [HttpPut, NoBody]
    [ProducesResponseType(204), ProducesResponseType<ApiError>(400), ProducesResponseType<ApiError>(404)]
    public async Task<IActionResult> Put(int reviewId, CancellationToken ct)
    {
        await likes.Set(reviewId, ct);
        return NoContent();
    }
    [HttpDelete, NoBody]
    [ProducesResponseType(204), ProducesResponseType<ApiError>(400), ProducesResponseType<ApiError>(404)]
    public async Task<IActionResult> Delete(int reviewId, CancellationToken ct)
    {
        await likes.Remove(reviewId, ct);
        return NoContent();
    }
    [HttpGet("me")]
    [ProducesResponseType<ReviewLikeStatusResponse>(200), ProducesResponseType<ApiError>(400), ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<ReviewLikeStatusResponse>> Get(int reviewId, CancellationToken ct) => Ok(new ReviewLikeStatusResponse(await likes.Has(reviewId, ct)));
}
