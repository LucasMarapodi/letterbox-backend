using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Forum;

namespace Tcc.Application.Forum;

public sealed class ForumWrites(IForum forum, QueryGames games, ForumQueries queries,
    ICurrentSession current, SessionAccess access, TimeProvider clock)
{
    public async Task<ForumTopicDetails> Topic(int gameId, string? title, string? text, CancellationToken ct)
    {
        var userId = (await access.Require(current, ct)).UserId;
        ForumQueries.RequireId(gameId);
        var draft = new ForumTopic(0, gameId, userId, title, text, clock.GetUtcNow());
        await games.GetAsync(gameId, ct);
        await access.Require(current, ct);
        return await forum.AddTopicAsync(draft, ct);
    }
    public async Task<ForumReplyDetails> Reply(int topicId, string? text, CancellationToken ct)
    {
        var userId = (await access.Require(current, ct)).UserId;
        ForumQueries.RequireId(topicId);
        var draft = new ForumReply(0, topicId, userId, text, clock.GetUtcNow());
        await queries.Topic(topicId, ct);
        await access.Require(current, ct);
        return await forum.AddReplyAsync(draft, ct);
    }
}
