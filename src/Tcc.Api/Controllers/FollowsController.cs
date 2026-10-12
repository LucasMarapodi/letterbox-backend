using Microsoft.AspNetCore.Authorization;
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
    public ActionResult<FollowStatusResponse> Follow(string userId)
    {
        users.Set(userId);
        return Ok(new FollowStatusResponse(true));
    }
    [Authorize, NoBody, HttpDelete("follow")]
    public IActionResult Unfollow(string userId) { users.Remove(userId); return NoContent(); }
    [Authorize, HttpGet("follow/me")]
    public ActionResult<FollowStatusResponse> Status(string userId) => Ok(new FollowStatusResponse(users.IsFollowing(userId)));
    [HttpGet("followers")]
    public ActionResult<IReadOnlyList<PublicUserResponse>> Followers(string userId) =>
        Ok(users.Followers(userId).Select(PublicUserResponse.From).ToArray());
    [HttpGet("following")]
    public ActionResult<IReadOnlyList<PublicUserResponse>> Following(string userId) =>
        Ok(users.Following(userId).Select(PublicUserResponse.From).ToArray());
}
