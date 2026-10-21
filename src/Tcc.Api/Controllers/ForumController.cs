using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Forum;

namespace Tcc.Api.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ApiError>(400)]
[ProducesResponseType<ApiError>(404)]
[ProducesResponseType<ApiError>(503)]
public sealed class ForumController(ForumQueries queries) : ControllerBase
{
    [HttpGet("api/v1/games/{gameId}/forum/topics")]
    public async Task<ActionResult<ForumPage<ForumTopicSummary>>> Topics(int gameId, CancellationToken ct, [FromQuery] int page = 1) =>
        Ok(await queries.Topics(gameId, page, ct));

    [HttpGet("api/v1/forum/topics/{topicId}")]
    public async Task<ActionResult<ForumTopicDetails>> Topic(int topicId, CancellationToken ct) => Ok(await queries.Topic(topicId, ct));

    [HttpGet("api/v1/forum/topics/{topicId}/replies")]
    public async Task<ActionResult<ForumPage<ForumReplyDetails>>> Replies(int topicId, CancellationToken ct, [FromQuery] int page = 1) =>
        Ok(await queries.Replies(topicId, page, ct));
}
