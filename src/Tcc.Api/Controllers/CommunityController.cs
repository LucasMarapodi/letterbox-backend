using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Queries;

namespace Tcc.Api.Controllers;

[ApiController, Route("api/v1/community")]
public sealed class CommunityController(RecentReviews recent) : ControllerBase
{
    [HttpGet("reviews/recent")]
    public ActionResult<IReadOnlyList<ReviewResponse>> Recent() => Ok(recent.Execute().Select(ReviewResponse.From).ToArray());
}
