using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Queries;

namespace Tcc.Api.Controllers;

[ApiController, Authorize, Route("api/v1/feed/following")]
public sealed class FeedController(FollowingFeed feed) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<ReviewResponse>> Get() => Ok(feed.Execute().Select(ReviewResponse.From).ToArray());
}
