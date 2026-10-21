using Tcc.Domain.Forum;

namespace Tcc.Application.Forum;

public sealed record ForumAuthor(int Id, string Username, string DisplayName, string? AvatarUrl);
public sealed record ForumGame(int Id, string Title, string? CoverUrl);
public sealed record ForumTopicSummary(int Id, int GameId, string Title, ForumAuthor Author,
    DateTimeOffset CreatedAt, int ReplyCount, DateTimeOffset LastActivityAt);
public sealed record ForumTopicDetails(int Id, int GameId, string Title, string Text, ForumAuthor Author,
    DateTimeOffset CreatedAt, int ReplyCount, DateTimeOffset LastActivityAt, ForumGame Game);
public sealed record ForumReplyDetails(int Id, int TopicId, ForumAuthor Author, string Text, DateTimeOffset CreatedAt);
public sealed record ForumPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasNextPage);

public interface IForum
{
    Task<ForumTopicDetails> AddTopicAsync(ForumTopic topic, CancellationToken ct);
    Task<ForumReplyDetails> AddReplyAsync(ForumReply reply, CancellationToken ct);
    Task<IReadOnlyList<ForumTopicSummary>> TopicsAsync(int gameId, int offset, int limit, CancellationToken ct);
    Task<ForumTopicDetails?> TopicAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ForumReplyDetails>> RepliesAsync(int topicId, int offset, int limit, CancellationToken ct);
}
