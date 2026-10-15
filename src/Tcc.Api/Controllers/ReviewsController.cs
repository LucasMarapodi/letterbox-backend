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
    public async Task<ActionResult<ReviewResponse>> Publish(string gameId, TextRequest request, CancellationToken cancellationToken)
    {
        var result = ReviewResponse.From(await publish.Execute(gameId, request.Text, cancellationToken));
        return Created($"/api/v1/reviews/{result.Id}", result);
    }
    [HttpGet("reviews/{reviewId}")]
    [ProducesResponseType<ReviewResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<ReviewResponse>> Get(string reviewId, CancellationToken cancellationToken) => Ok(ReviewResponse.From(await reviews.ExecuteAsync(reviewId, cancellationToken)));

    [Authorize]
    [HttpPatch("reviews/{reviewId}")]
    [ProducesResponseType<ReviewResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(403)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<ReviewResponse>> Edit(string reviewId, TextRequest request, CancellationToken cancellationToken) =>
        Ok(ReviewResponse.From(await edit.Execute(reviewId, request.Text, cancellationToken)));

    [HttpGet("games/{gameId}/reviews")]
    [ProducesResponseType<IReadOnlyList<ReviewResponse>>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<IReadOnlyList<ReviewResponse>>> ForGame(string gameId, CancellationToken cancellationToken) =>
        Ok((await queries.ForGame(gameId, cancellationToken)).Select(ReviewResponse.From).ToArray());
    [HttpGet("users/{userId}/reviews")]
    [ProducesResponseType<IReadOnlyList<ReviewResponse>>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<IReadOnlyList<ReviewResponse>>> ForUser(int userId, CancellationToken cancellationToken) =>
        Ok((await queries.ForUser(userId, cancellationToken)).Select(ReviewResponse.From).ToArray());
    [Authorize]
    [HttpGet("games/{gameId}/reviews/me")]
    [ProducesResponseType<MyReviewResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<MyReviewResponse>> Mine(string gameId, CancellationToken cancellationToken)
    {
        var mine = await queries.Mine(gameId, cancellationToken);
        return Ok(new MyReviewResponse(mine is null ? null : ReviewResponse.From(mine)));
    }
    [HttpGet("games/{gameId}/opinions")]
    [ProducesResponseType<GameOpinionsResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<GameOpinionsResponse>> Opinions(string gameId, CancellationToken cancellationToken)
    {
        var result = await queries.Opinions(gameId, cancellationToken);
        return Ok(new GameOpinionsResponse(result.AverageRating, result.Reviews.Select(ReviewResponse.From).ToArray()));
    }
}
