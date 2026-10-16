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
    public ActionResult<IReadOnlyList<CommentResponse>> Get(string reviewId) =>
        Ok(comments.Execute(reviewId).Select(CommentResponse.From).ToArray());
    [Authorize]
    [HttpPost]
    [ProducesResponseType<CommentResponse>(201)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<CommentResponse> Post(string reviewId, TextRequest request)
    {
        var comment = CommentResponse.From(publish.Execute(reviewId, request.Text));
        return Created($"/api/v1/reviews/{reviewId}/comments", comment);
    }
}
