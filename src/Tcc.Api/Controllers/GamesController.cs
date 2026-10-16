using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Catalog;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1/games")]
public sealed class GamesController(QueryGames games, LocalRatingAverage averages) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GameResponse>>(200)]
    [ProducesResponseType<ApiError>(400)]
    public ActionResult<IReadOnlyList<GameResponse>> List([FromQuery] string? term, [FromQuery] string? sort) =>
        Ok(games.List(term, sort).Select(game => GameResponse.From(game, averages.ForGame(game.Id))).ToArray());

    [HttpGet("{gameId}")]
    [ProducesResponseType<GameResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<GameResponse> Get(string gameId) => Ok(GameResponse.From(games.Get(gameId), averages.ForGame(gameId)));
}
