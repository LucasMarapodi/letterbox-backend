using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Catalog;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1/games")]
public sealed class GamesController(QueryGames games) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<GameResponse>> List([FromQuery] string? term, [FromQuery] string? sort) =>
        Ok(games.List(term, sort).Select(game => GameResponse.From(game)).ToArray());

    [HttpGet("{gameId}")]
    public ActionResult<GameResponse> Get(string gameId) => Ok(GameResponse.From(games.Get(gameId)));
}
