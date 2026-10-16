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
    public ActionResult<FavoriteListsResponse> List()
    {
        var lists = favorites.List();
        return Ok(FavoriteListsResponse.From(lists, averages.ForGame));
    }
    [HttpGet("{kind}/{targetId}")]
    public ActionResult<FavoriteStatusResponse> Get(string kind, string targetId)
    {
        var favorite = favorites.Get(kind, targetId);
        return Ok(new FavoriteStatusResponse(favorite is null ? null : FavoriteResponse.From(favorite)));
    }
    [NoBody, HttpPut("{kind}/{targetId}")]
    public ActionResult<FavoriteResponse> Put(string kind, string targetId) => Ok(FavoriteResponse.From(favorites.Set(kind, targetId)));
    [NoBody, HttpDelete("{kind}/{targetId}")]
    public IActionResult Delete(string kind, string targetId) { favorites.Remove(kind, targetId); return NoContent(); }
}
