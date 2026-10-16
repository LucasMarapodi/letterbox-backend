using Tcc.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;
using Tcc.Application.Social;

namespace Tcc.Api.Controllers;

[ApiController, Authorize, Route("api/v1/favorites")]
public sealed class FavoritesController(PersonalFavorites favorites, LocalRatingAverage averages) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FavoriteListsResponse>> List(CancellationToken cancellationToken)
    {
        var lists = await favorites.List(cancellationToken);
        var values = await averages.ForGames(lists.Games.Select(game => game.Id), cancellationToken);
        return Ok(FavoriteListsResponse.From(lists, id => values.GetValueOrDefault(id)));
    }
    [HttpGet("{kind}/{targetId}")]
    [ProducesResponseType<FavoriteStatusResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<FavoriteStatusResponse>> Get(string kind, string targetId, CancellationToken cancellationToken)
    {
        var favorite = await favorites.Get(kind, targetId, cancellationToken);
        return Ok(new FavoriteStatusResponse(favorite is null ? null : FavoriteResponse.From(favorite)));
    }
    [NoBody, HttpPut("{kind}/{targetId}")]
    [ProducesResponseType<FavoriteResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<FavoriteResponse>> Put(string kind, string targetId, CancellationToken cancellationToken) => Ok(FavoriteResponse.From(await favorites.Set(kind, targetId, cancellationToken)));
    [NoBody, HttpDelete("{kind}/{targetId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<IActionResult> Delete(string kind, string targetId, CancellationToken cancellationToken) { await favorites.Remove(kind, targetId, cancellationToken); return NoContent(); }
}
