using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Catalog;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1/games")]
public sealed class GamesController(IAsyncGameCatalog games, CatalogUsage usage, LocalRatingAverage averages) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<GamePageResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(502)]
    [ProducesResponseType<ApiError>(503)]
    [ProducesResponseType<ApiError>(504)]
    public async Task<ActionResult<GamePageResponse>> List([FromQuery] string? term, [FromQuery] string? sort,
        CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await games.SearchAsync(CatalogSearch.Create(term, sort, page, pageSize), cancellationToken);
        usage.Include(result.Metadata);
        return Ok(new GamePageResponse(result.Items.Select(item => GameResponse.From(item.Game, averages.ForGame(item.Game.Id))).ToArray(),
            result.Query.Page, result.Query.PageSize, result.HasNextPage));
    }

    [HttpGet("{gameId}")]
    [ProducesResponseType<GameResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(502)]
    [ProducesResponseType<ApiError>(503)]
    [ProducesResponseType<ApiError>(504)]
    public async Task<ActionResult<GameResponse>> Get(string gameId, CancellationToken cancellationToken)
    {
        var item = await games.GetAsync(gameId, cancellationToken) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
        usage.Include(item.Metadata);
        return Ok(GameResponse.From(item.Game, averages.ForGame(gameId)));
    }
}
