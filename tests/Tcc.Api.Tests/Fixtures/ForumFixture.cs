using Tcc.Application.Forum;
using Tcc.Domain.Forum;

namespace Tcc.Api.Tests.Fixtures;

// Permission/documentation fixtures only. SQL and writes are verified with the isolated PostgreSQL host.
internal sealed class ForumFixture : IForum
{
    public Task<IReadOnlyList<ForumTopicSummary>> TopicsAsync(int gameId, int offset, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ForumTopicSummary>>([]);
    public Task<IReadOnlyList<ForumReplyDetails>> RepliesAsync(int topicId, int offset, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ForumReplyDetails>>([]);
    public Task<ForumTopicDetails?> TopicAsync(int id, CancellationToken ct) => Task.FromResult<ForumTopicDetails?>(id == 1 ?
        new(1, 1, "Fixture topic", "Initial text", new(1, "ana", "Ana", null), DateTimeOffset.UnixEpoch, 0,
            DateTimeOffset.UnixEpoch, new(1, "Fixture game", null)) : null);
    public Task<ForumTopicDetails> AddTopicAsync(ForumTopic topic, CancellationToken ct) => throw new NotSupportedException();
    public Task<ForumReplyDetails> AddReplyAsync(ForumReply reply, CancellationToken ct) => throw new NotSupportedException();
}
