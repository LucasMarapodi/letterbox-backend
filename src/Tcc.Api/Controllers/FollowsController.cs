using Microsoft.AspNetCore.Authorization;
using Tcc.Api.Filters;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Social;

namespace Tcc.Api.Controllers;

public sealed record FollowStatusResponse(bool Following);

[ApiController]
[Route("api/v1/users/{userId}")]
public sealed class FollowsController(FollowUsers users) : ControllerBase
{
    [Authorize, NoBody, HttpPut("follow")]
    [ProducesResponseType<FollowStatusResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<FollowStatusResponse>> Follow(int userId, CancellationToken cancellationToken)
    {
        await users.Set(userId, cancellationToken);
        return Ok(new FollowStatusResponse(true));
    }
    [Authorize, NoBody, HttpDelete("follow")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<IActionResult> Unfollow(int userId, CancellationToken cancellationToken) { await users.Remove(userId, cancellationToken); return NoContent(); }
    [Authorize, HttpGet("follow/me")]
    [ProducesResponseType<FollowStatusResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<FollowStatusResponse>> Status(int userId, CancellationToken cancellationToken) => Ok(new FollowStatusResponse(await users.IsFollowing(userId, cancellationToken)));
    [HttpGet("followers")]
    [ProducesResponseType<IReadOnlyList<PublicUserResponse>>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<IReadOnlyList<PublicUserResponse>>> Followers(int userId, CancellationToken cancellationToken) =>
        Ok((await users.Followers(userId, cancellationToken)).Select(PublicUserResponse.From).ToArray());
    [HttpGet("following")]
    [ProducesResponseType<IReadOnlyList<PublicUserResponse>>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<IReadOnlyList<PublicUserResponse>>> Following(int userId, CancellationToken cancellationToken) =>
        Ok((await users.Following(userId, cancellationToken)).Select(PublicUserResponse.From).ToArray());
}
