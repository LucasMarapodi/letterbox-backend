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
        return Ok(FavoriteListsResponse.From(lists, averages.ForGame));
    }
    [HttpGet("{kind}/{targetId}")]
    [ProducesResponseType<FavoriteStatusResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<FavoriteStatusResponse> Get(string kind, string targetId)
    {
        var favorite = favorites.Get(kind, targetId);
        return Ok(new FavoriteStatusResponse(favorite is null ? null : FavoriteResponse.From(favorite)));
    }
    [NoBody, HttpPut("{kind}/{targetId}")]
    [ProducesResponseType<FavoriteResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<FavoriteResponse> Put(string kind, string targetId) => Ok(FavoriteResponse.From(favorites.Set(kind, targetId)));
    [NoBody, HttpDelete("{kind}/{targetId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public IActionResult Delete(string kind, string targetId) { favorites.Remove(kind, targetId); return NoContent(); }
}

