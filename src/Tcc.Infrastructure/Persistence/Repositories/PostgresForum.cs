using Microsoft.EntityFrameworkCore;
using Tcc.Application.Forum;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresForum(Lazy<TccDbContext> context) : IForum
{
    private static IQueryable<Activity> Activities(TccDbContext db) => db.ForumReplies.AsNoTracking()
        .GroupBy(reply => reply.TopicId)
        .Select(group => new Activity { TopicId = group.Key, Count = group.Count(), Last = group.Max(reply => reply.CreatedAt) });

    public Task<IReadOnlyList<ForumTopicSummary>> TopicsAsync(int gameId, int offset, int limit, CancellationToken ct) =>
        OpinionStorage.Run<IReadOnlyList<ForumTopicSummary>>(async () =>
        {
            var db = context.Value;
            var query = from topic in db.ForumTopics.AsNoTracking()
                        where topic.GameId == gameId
                        join author in db.Users.AsNoTracking() on topic.UserId equals author.Id
                        join activity in Activities(db) on topic.Id equals activity.TopicId into grouped
                        from activity in grouped.DefaultIfEmpty()
                        let last = activity.Last > topic.CreatedAt ? activity.Last.Value : topic.CreatedAt
                        orderby last descending, topic.Id descending
                        select new ForumTopicSummary(topic.Id, topic.GameId, topic.Title,
                            new ForumAuthor(author.Id, author.Username, author.Profile.DisplayName, author.Profile.AvatarUrl),
                            topic.CreatedAt, activity.Count ?? 0, last);
            return await query.Skip(offset).Take(limit).ToArrayAsync(ct);
        });

    public Task<ForumTopicDetails?> TopicAsync(int id, CancellationToken ct) => OpinionStorage.Run(async () =>
    {
        var db = context.Value;
        var query = from topic in db.ForumTopics.AsNoTracking()
                    where topic.Id == id
                    join author in db.Users.AsNoTracking() on topic.UserId equals author.Id
                    join game in db.Games.AsNoTracking() on topic.GameId equals game.Id
                    join activity in Activities(db) on topic.Id equals activity.TopicId into grouped
                    from activity in grouped.DefaultIfEmpty()
                    select new ForumTopicDetails(topic.Id, topic.GameId, topic.Title, topic.Text,
                        new ForumAuthor(author.Id, author.Username, author.Profile.DisplayName, author.Profile.AvatarUrl),
                        topic.CreatedAt, activity.Count ?? 0,
                        activity.Last > topic.CreatedAt ? activity.Last.Value : topic.CreatedAt,
                        new ForumGame(game.Id, game.Title, game.CoverUrl));
        return await query.SingleOrDefaultAsync(ct);
    });

    public Task<IReadOnlyList<ForumReplyDetails>> RepliesAsync(int topicId, int offset, int limit, CancellationToken ct) =>
        OpinionStorage.Run<IReadOnlyList<ForumReplyDetails>>(async () =>
        {
            var db = context.Value;
            var query = from reply in db.ForumReplies.AsNoTracking()
                        where reply.TopicId == topicId
                        join author in db.Users.AsNoTracking() on reply.UserId equals author.Id
                        orderby reply.CreatedAt, reply.Id
                        select new ForumReplyDetails(reply.Id, reply.TopicId,
                            new ForumAuthor(author.Id, author.Username, author.Profile.DisplayName, author.Profile.AvatarUrl), reply.Text, reply.CreatedAt);
            return await query.Skip(offset).Take(limit).ToArrayAsync(ct);
        });

    private sealed class Activity
    {
        public int TopicId { get; init; }
        public int? Count { get; init; }
        public DateTimeOffset? Last { get; init; }
    }
}
