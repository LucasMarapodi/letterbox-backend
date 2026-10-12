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
    public ActionResult<IReadOnlyList<CommentResponse>> Get(string reviewId) =>
        Ok(comments.Execute(reviewId).Select(CommentResponse.From).ToArray());
    [Authorize]
    [HttpPost]
    public ActionResult<CommentResponse> Post(string reviewId, TextRequest request)
    {
        var comment = CommentResponse.From(publish.Execute(reviewId, request.Text));
        return Created($"/api/v1/reviews/{reviewId}/comments", comment);
    }
}
