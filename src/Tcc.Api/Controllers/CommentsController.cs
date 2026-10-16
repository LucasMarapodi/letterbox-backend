using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1/reviews/{reviewId}/comments")]
public sealed class CommentsController(QueryComments comments, PublishComment publish) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CommentResponse>>(200)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<IReadOnlyList<CommentResponse>>> Get(int reviewId, CancellationToken cancellationToken) =>
        Ok((await comments.Execute(reviewId, cancellationToken)).Select(CommentResponse.From).ToArray());
    [Authorize]
    [HttpPost]
    [ProducesResponseType<CommentResponse>(201)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<CommentResponse>> Post(int reviewId, TextRequest request, CancellationToken cancellationToken)
    {
        var comment = CommentResponse.From(await publish.Execute(reviewId, request.Text, cancellationToken));
        return Created($"/api/v1/reviews/{reviewId}/comments", comment);
    }
}
