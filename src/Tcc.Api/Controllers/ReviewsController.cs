using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;
using Tcc.Domain;

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
    public async Task<ActionResult<ReviewResponse>> Publish(int gameId, ReviewCreateRequest request, CancellationToken cancellationToken)
    {
        var result = ReviewResponse.From(await publish.Execute(gameId, request.Text, request.Rating, cancellationToken, title: request.Title));
        return Created($"/api/v1/reviews/{result.Id}", result);
    }
    [HttpGet("reviews/{reviewId}")]
    [ProducesResponseType<ReviewResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<ReviewResponse>> Get(int reviewId, CancellationToken cancellationToken) => Ok(ReviewResponse.From(await reviews.ExecuteAsync(reviewId, cancellationToken)));

    [Authorize]
    [HttpPatch("reviews/{reviewId}")]
    [ProducesResponseType<ReviewResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(403)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<ReviewResponse>> Edit(int reviewId, ReviewPatchRequest request, CancellationToken cancellationToken)
    {
        if (request.RatingSpecified && request.Rating is null) throw new DomainValidationException("Nota não pode ser null.");
        return Ok(ReviewResponse.From(await edit.Execute(reviewId, request.TextSpecified, request.Text, request.Rating, cancellationToken, request.TitleSpecified, request.Title)));
    }

    [HttpGet("games/{gameId}/reviews")]
    [ProducesResponseType<IReadOnlyList<ReviewResponse>>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<IReadOnlyList<ReviewResponse>>> ForGame(int gameId, CancellationToken cancellationToken) =>
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
    public async Task<ActionResult<MyReviewResponse>> Mine(int gameId, CancellationToken cancellationToken)
    {
        var mine = await queries.Mine(gameId, cancellationToken);
        return Ok(new MyReviewResponse(mine is null ? null : ReviewResponse.From(mine)));
    }
    [HttpGet("games/{gameId}/opinions")]
    [ProducesResponseType<GameOpinionsResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<GameOpinionsResponse>> Opinions(int gameId, CancellationToken cancellationToken)
    {
        var result = await queries.Opinions(gameId, cancellationToken);
        return Ok(new GameOpinionsResponse(result.AverageRating, result.Reviews.Select(ReviewResponse.From).ToArray()));
    }
}
