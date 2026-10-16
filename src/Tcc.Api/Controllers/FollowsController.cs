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
    public ActionResult<FollowStatusResponse> Follow(string userId)
    {
        users.Set(userId);
        return Ok(new FollowStatusResponse(true));
    }
    [Authorize, NoBody, HttpDelete("follow")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public IActionResult Unfollow(string userId) { users.Remove(userId); return NoContent(); }
    [Authorize, HttpGet("follow/me")]
    [ProducesResponseType<FollowStatusResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<FollowStatusResponse> Status(string userId) => Ok(new FollowStatusResponse(users.IsFollowing(userId)));
    [HttpGet("followers")]
    [ProducesResponseType<IReadOnlyList<PublicUserResponse>>(200)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<IReadOnlyList<PublicUserResponse>> Followers(string userId) =>
        Ok(users.Followers(userId).Select(PublicUserResponse.From).ToArray());
    [HttpGet("following")]
    [ProducesResponseType<IReadOnlyList<PublicUserResponse>>(200)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<IReadOnlyList<PublicUserResponse>> Following(string userId) =>
        Ok(users.Following(userId).Select(PublicUserResponse.From).ToArray());
}
