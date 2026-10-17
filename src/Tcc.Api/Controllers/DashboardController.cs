using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;
using Tcc.Application;

namespace Tcc.Api.Controllers;

[ApiController, Authorize, Route("api/v1/dashboard")]
public sealed class DashboardController(PersonalDashboard dashboard, LocalRatingAverage averages) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken cancellationToken)
    {
        var result = await dashboard.Execute(cancellationToken);
        var values = await averages.ForGames(result.Favorites.Games.Select(game => game.Id), cancellationToken);
        return Ok(new DashboardResponse(FavoriteListsResponse.From(result.Favorites, id => values.GetValueOrDefault(id)),
            result.Feed.Select(ReviewResponse.From).ToArray()));
    }
}
