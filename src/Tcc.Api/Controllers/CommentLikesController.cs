using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Api.Filters;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController, Authorize, Route("api/v1/comments/{commentId}/like")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CommentLikesController(PersonalCommentLike likes) : ControllerBase
{
    [HttpPut, NoBody]
    [ProducesResponseType(204), ProducesResponseType<ApiError>(400), ProducesResponseType<ApiError>(404)]
    public async Task<IActionResult> Put(int commentId, CancellationToken ct)
    {
        await likes.Set(commentId, ct);
        return NoContent();
    }
    [HttpDelete, NoBody]
    [ProducesResponseType(204), ProducesResponseType<ApiError>(400), ProducesResponseType<ApiError>(404)]
    public async Task<IActionResult> Delete(int commentId, CancellationToken ct)
    {
        await likes.Remove(commentId, ct);
        return NoContent();
    }
    [HttpGet("me")]
    [ProducesResponseType<CommentLikeStatusResponse>(200), ProducesResponseType<ApiError>(400), ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<CommentLikeStatusResponse>> Get(int commentId, CancellationToken ct) => Ok(new CommentLikeStatusResponse(await likes.Has(commentId, ct)));
}
