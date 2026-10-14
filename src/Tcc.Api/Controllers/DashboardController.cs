using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;
using Tcc.Application.Queries;

namespace Tcc.Api.Controllers;

public sealed record DashboardResponse(FavoriteListsResponse Favorites, IReadOnlyList<ReviewResponse> Feed);

[ApiController, Authorize, Route("api/v1/dashboard")]
public sealed class DashboardController(PersonalDashboard dashboard, LocalRatingAverage averages) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken cancellationToken)
    {
        var result = await dashboard.Execute(cancellationToken);
        return Ok(new DashboardResponse(FavoriteListsResponse.From(result.Favorites, averages.ForGame),
            result.Feed.Select(ReviewResponse.From).ToArray()));
    }
}

