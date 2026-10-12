using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ReviewsController(PublishReview publish, GetReview reviews, QueryReviews queries, EditReview edit) : ControllerBase
{
    [Authorize]
    [HttpPost("games/{gameId}/reviews")]
    [ProducesResponseType<ReviewResponse>(201)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    [ProducesResponseType<ApiError>(409)]
    public ActionResult<ReviewResponse> Publish(string gameId, TextRequest request)
    {
        var result = ReviewResponse.From(publish.Execute(gameId, request.Text));
        return Created($"/api/v1/reviews/{result.Id}", result);
    }
    [HttpGet("reviews/{reviewId}")]
    [ProducesResponseType<ReviewResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<ReviewResponse> Get(string reviewId) => Ok(ReviewResponse.From(reviews.Execute(reviewId)));

    [Authorize]
    [HttpPatch("reviews/{reviewId}")]
    [ProducesResponseType<ReviewResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(403)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<ReviewResponse> Edit(string reviewId, TextRequest request) =>
        Ok(ReviewResponse.From(edit.Execute(reviewId, request.Text)));

    [HttpGet("games/{gameId}/reviews")]
    [ProducesResponseType<IReadOnlyList<ReviewResponse>>(200)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<IReadOnlyList<ReviewResponse>> ForGame(string gameId) =>
        Ok(queries.ForGame(gameId).Select(ReviewResponse.From).ToArray());
    [HttpGet("users/{userId}/reviews")]
    [ProducesResponseType<IReadOnlyList<ReviewResponse>>(200)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<IReadOnlyList<ReviewResponse>> ForUser(string userId) =>
        Ok(queries.ForUser(userId).Select(ReviewResponse.From).ToArray());
    [Authorize]
    [HttpGet("games/{gameId}/reviews/me")]
    [ProducesResponseType<MyReviewResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<MyReviewResponse> Mine(string gameId)
    {
        var mine = queries.Mine(gameId);
        return Ok(new MyReviewResponse(mine is null ? null : ReviewResponse.From(mine)));
    }
    [HttpGet("games/{gameId}/opinions")]
    [ProducesResponseType<GameOpinionsResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<GameOpinionsResponse> Opinions(string gameId)
    {
        var result = queries.Opinions(gameId);
        return Ok(new GameOpinionsResponse(result.AverageRating, result.Reviews.Select(ReviewResponse.From).ToArray()));
    }
}
