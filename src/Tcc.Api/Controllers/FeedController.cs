using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Social;

namespace Tcc.Api.Controllers;

[ApiController, Authorize, Route("api/v1/feed/following")]
public sealed class FeedController(FollowingFeed feed) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReviewResponse>>> Get(CancellationToken cancellationToken) => Ok((await feed.Execute(cancellationToken)).Select(ReviewResponse.From).ToArray());
}
