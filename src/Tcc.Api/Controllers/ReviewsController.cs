using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ReviewsController(PublishReview publish, GetReview reviews) : ControllerBase
{
    [Authorize]
    [HttpPost("games/{gameId}/reviews")]
    public ActionResult<ReviewResponse> Publish(string gameId, TextRequest request)
    {
        var result = ReviewResponse.From(publish.Execute(gameId, request.Text));
        return Created($"/api/v1/reviews/{result.Id}", result);
    }
    [HttpGet("reviews/{reviewId}")]
    public ActionResult<ReviewResponse> Get(string reviewId) => Ok(ReviewResponse.From(reviews.Execute(reviewId)));
}
