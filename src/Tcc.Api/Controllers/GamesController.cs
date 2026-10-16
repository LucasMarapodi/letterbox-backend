using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Catalog;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1/games")]
public sealed class GamesController(IAsyncGameCatalog games, DiscoverGame discovery, CatalogUsage usage, LocalRatingAverage averages, CatalogFilters filters) : ControllerBase
{
    [HttpGet("filters")]
    [ProducesResponseType<CatalogFilterOptions>(200)]
    [ProducesResponseType<ApiError>(503)]
    public async Task<ActionResult<CatalogFilterOptions>> Filters(CancellationToken cancellationToken) => Ok(await filters.GetAsync(cancellationToken));
    [HttpGet("igdb/{igdbId}")]
    [ProducesResponseType<GameResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    [ProducesResponseType<ApiError>(502)]
    [ProducesResponseType<ApiError>(503)]
    [ProducesResponseType<ApiError>(504)]
    public async Task<ActionResult<GameResponse>> Discover(long igdbId, CancellationToken cancellationToken)
    {
        var item = await discovery.Execute(igdbId, cancellationToken) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
        usage.Include(item.Metadata);
        return Ok(GameResponse.From(item.Game, await averages.ForGame(item.Game.Id, cancellationToken)));
    }

    [HttpGet]
    [ProducesResponseType<GamePageResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(502)]
    [ProducesResponseType<ApiError>(503)]
    [ProducesResponseType<ApiError>(504)]
    public async Task<ActionResult<GamePageResponse>> List([FromQuery] string? term, [FromQuery] string? sort,
        CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? genreId = null, [FromQuery] int? platformId = null)
    {
        var result = await games.SearchAsync(CatalogSearch.Create(term, sort, page, pageSize, genreId, platformId), cancellationToken);
        usage.Include(result.Metadata);
        var values = await averages.ForGames(result.Items.Select(item => item.Game.Id), cancellationToken);
        return Ok(new GamePageResponse(result.Items.Select(item => GameResponse.From(item.Game, values.GetValueOrDefault(item.Game.Id))).ToArray(),
            result.Query.Page, result.Query.PageSize, result.HasNextPage));
    }

    [HttpGet("{gameId}")]
    [ProducesResponseType<GameResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(502)]
    [ProducesResponseType<ApiError>(503)]
    [ProducesResponseType<ApiError>(504)]
    public async Task<ActionResult<GameResponse>> Get(int gameId, CancellationToken cancellationToken)
    {
        var item = await games.GetAsync(gameId, cancellationToken) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
        usage.Include(item.Metadata);
        return Ok(GameResponse.From(item.Game, await averages.ForGame(gameId, cancellationToken)));
    }
}
