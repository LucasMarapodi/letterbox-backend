using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Opinions;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1/reviews/{reviewId}/comments")]
public sealed class CommentsController(QueryComments comments) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<CommentResponse>> Get(string reviewId) =>
        Ok(comments.Execute(reviewId).Select(CommentResponse.From).ToArray());
}
