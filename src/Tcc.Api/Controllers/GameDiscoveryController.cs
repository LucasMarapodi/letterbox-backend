using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Catalog;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1/games")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ApiError>(400)]
[ProducesResponseType<ApiError>(503)]
public sealed class GameDiscoveryController(GameDiscovery discovery) : ControllerBase
{
    [HttpGet("top-rated")]
    [ProducesResponseType<DiscoveryPageResponse<TopRatedGameResponse>>(200)]
    public async Task<ActionResult<DiscoveryPageResponse<TopRatedGameResponse>>> TopRated(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var rows = await discovery.Rank(false, page, pageSize, ct);
        return Ok(new DiscoveryPageResponse<TopRatedGameResponse>(rows.Take(pageSize).Select(row =>
            new TopRatedGameResponse(GameResponse.From(row.Game, row.AverageRating), row.AverageRating, row.RatingCount, row.LastRatedAt)).ToArray(), page, pageSize, rows.Count > pageSize));
    }

    [HttpGet("trending")]
    [ProducesResponseType<DiscoveryPageResponse<TrendingGameResponse>>(200)]
    public async Task<ActionResult<DiscoveryPageResponse<TrendingGameResponse>>> Trending(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var rows = await discovery.Rank(true, page, pageSize, ct);
        return Ok(new DiscoveryPageResponse<TrendingGameResponse>(rows.Take(pageSize).Select(row =>
            new TrendingGameResponse(GameResponse.From(row.Game, row.AverageRating), row.LastRatedAt)).ToArray(), page, pageSize, rows.Count > pageSize));
    }

    [Authorize]
    [HttpGet("recommendations/me")]
    [ProducesResponseType<RecommendationsPageResponse>(200)]
    [ProducesResponseType<ApiError>(401)]
    public async Task<ActionResult<RecommendationsPageResponse>> Recommendations(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await discovery.Recommend(page, pageSize, ct);
        return Ok(new RecommendationsPageResponse(result.Items.Take(pageSize).Select(row =>
            new RecommendedGameResponse(GameResponse.From(row.Game, row.AverageRating), row.Score, row.MatchedGenres, row.MatchedThemes)).ToArray(),
            page, pageSize, result.Items.Count > pageSize, !result.HasPreferences ? "no_preferences" : result.Items.Count == 0 ? "no_matches" : null));
    }
}
