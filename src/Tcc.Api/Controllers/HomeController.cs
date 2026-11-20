using Microsoft.AspNetCore.Mvc;
using Tcc.Application.Catalog;
using Tcc.Api.Contracts;

namespace Tcc.Api.Controllers;

[ApiController, Route("api/v1/home")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HomeController(GameDiscovery discovery) : ControllerBase
{
    [HttpGet("featured")]
    [ProducesResponseType<FeaturedGameResponse>(200)]
    [ProducesResponseType<ApiError>(503)]
    public async Task<ActionResult<FeaturedGameResponse>> Featured(CancellationToken ct)
    {
        var result = await discovery.Featured(ct);
        return Ok(new FeaturedGameResponse(result.Game is { } game
            ? new FeaturedGameDetails(game.Id, game.Title, game.Summary, game.CoverUrl, game.BannerUrl!) : null, result.SelectionReason));
    }
}
