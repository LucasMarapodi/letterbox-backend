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
    public ActionResult<MyRatingResponse> Get(string gameId)
    {
        var rating = ratings.Get(gameId);
        return Ok(new MyRatingResponse(rating is null ? null : RatingResponse.From(rating)));
    }
    [HttpPut]
    public ActionResult<RatingResponse> Set(string gameId, RatingRequest request) =>
        Ok(RatingResponse.From(ratings.Set(gameId, request.Value.GetValueOrDefault())));
}
