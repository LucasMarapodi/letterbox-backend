using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Tcc.Api.Contracts;
using Tcc.Application.Forum;

namespace Tcc.Api.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ApiError>(400)]
[ProducesResponseType<ApiError>(404)]
[ProducesResponseType<ApiError>(503)]
public sealed class ForumController(ForumQueries queries, ForumWrites writes) : ControllerBase
{
    [HttpGet("api/v1/games/{gameId}/forum/topics")]
    [ProducesResponseType<ForumPage<ForumTopicSummary>>(200)]
    public async Task<ActionResult<ForumPage<ForumTopicSummary>>> Topics(int gameId, CancellationToken ct, [FromQuery] int page = 1) =>
        Ok(await queries.Topics(gameId, page, ct));

    [HttpGet("api/v1/forum/topics/{topicId}")]
    [ProducesResponseType<ForumTopicDetails>(200)]
    public async Task<ActionResult<ForumTopicDetails>> Topic(int topicId, CancellationToken ct) => Ok(await queries.Topic(topicId, ct));

    [HttpGet("api/v1/forum/topics/{topicId}/replies")]
    [ProducesResponseType<ForumPage<ForumReplyDetails>>(200)]
    public async Task<ActionResult<ForumPage<ForumReplyDetails>>> Replies(int topicId, CancellationToken ct, [FromQuery] int page = 1) =>
        Ok(await queries.Replies(topicId, page, ct));

    [Authorize]
    [HttpPost("api/v1/games/{gameId}/forum/topics")]
    [ProducesResponseType<ForumTopicDetails>(201)]
    public async Task<ActionResult<ForumTopicDetails>> CreateTopic(int gameId, ForumTopicRequest request, CancellationToken ct)
    {
        var topic = await writes.Topic(gameId, request.Title, request.Text, ct);
        return Created($"/api/v1/forum/topics/{topic.Id}", topic);
    }

    [Authorize]
    [HttpPost("api/v1/forum/topics/{topicId}/replies")]
    [ProducesResponseType<ForumReplyDetails>(201)]
    public async Task<ActionResult<ForumReplyDetails>> CreateReply(int topicId, ForumReplyRequest request, CancellationToken ct)
    {
        var reply = await writes.Reply(topicId, request.Text, ct);
        return Created($"/api/v1/forum/topics/{topicId}/replies", reply);
    }
}
