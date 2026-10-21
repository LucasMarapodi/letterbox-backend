using Tcc.Application.Catalog;
using Tcc.Domain;

namespace Tcc.Application.Forum;

public sealed class ForumQueries(IForum forum, QueryGames games)
{
    public async Task<ForumPage<ForumTopicSummary>> Topics(int gameId, int page, CancellationToken ct)
    {
        var offset = Offset(page); RequireId(gameId);
        await games.GetAsync(gameId, ct);
        return Page(await forum.TopicsAsync(gameId, offset, 21, ct), page);
    }
    public async Task<ForumTopicDetails> Topic(int id, CancellationToken ct)
    {
        RequireId(id);
        return await forum.TopicAsync(id, ct) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
    }
    public async Task<ForumPage<ForumReplyDetails>> Replies(int topicId, int page, CancellationToken ct)
    {
        var offset = Offset(page);
        await Topic(topicId, ct);
        return Page(await forum.RepliesAsync(topicId, offset, 21, ct), page);
    }
    internal static void RequireId(int id)
    { if (id <= 0) throw new DomainValidationException("ID do fórum inválido."); }
    private static int Offset(int page)
    {
        if (page < 1 || ((long)page - 1) * 20 > int.MaxValue)
            throw new DomainValidationException("Página do fórum inválida.");
        return (page - 1) * 20;
    }
    private static ForumPage<T> Page<T>(IReadOnlyList<T> rows, int page) => new(rows.Take(20).ToArray(), page, 20, rows.Count > 20);
}
