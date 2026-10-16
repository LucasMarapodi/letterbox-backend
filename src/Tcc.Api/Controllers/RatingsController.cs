using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/games/{gameId}/rating/me")]
public sealed class RatingsController(PersonalRating ratings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<MyRatingResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<MyRatingResponse>> Get(int gameId, CancellationToken cancellationToken)
    {
        var rating = await ratings.Get(gameId, cancellationToken);
        return Ok(new MyRatingResponse(rating is null ? null : RatingResponse.From(rating)));
    }
    [HttpPut]
    [ProducesResponseType<RatingResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<RatingResponse>> Set(int gameId, RatingRequest request, CancellationToken cancellationToken) =>
        Ok(RatingResponse.From(await ratings.Set(gameId, request.Value.GetValueOrDefault(), cancellationToken)));
}
