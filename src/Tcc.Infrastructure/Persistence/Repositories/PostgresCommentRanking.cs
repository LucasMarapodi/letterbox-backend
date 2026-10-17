using Microsoft.EntityFrameworkCore;
using Tcc.Application.Opinions;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresCommentRanking(Lazy<TccDbContext> context) : ICommentRanking
{
    public Task<IReadOnlyList<RankedCommentDetails>> ReadAsync(DateTimeOffset from, DateTimeOffset to,
        int offset, int limit, CancellationToken cancellationToken = default) =>
        OpinionStorage.Run<IReadOnlyList<RankedCommentDetails>>(async () =>
        {
            var db = context.Value;
            var activity = db.CommentLikes.AsNoTracking()
                .Where(like => like.CreatedAt >= from && like.CreatedAt <= to)
                .GroupBy(like => like.CommentId)
                .Select(group => new { CommentId = group.Key, Count = group.Count(), Last = group.Max(like => like.CreatedAt) });
            var query = from likes in activity
                        join comment in db.Comments.AsNoTracking() on likes.CommentId equals comment.Id
                        join review in db.Reviews.AsNoTracking() on comment.ReviewId equals review.Id
                        join game in db.Games.AsNoTracking() on review.GameId equals game.Id
                        join author in db.Users.AsNoTracking() on comment.UserId equals author.Id
                        orderby likes.Count descending, likes.Last descending, comment.Id
                        select new RankedCommentDetails(comment.Id, review.Id, game.Id, comment.ParentCommentId,
                            comment.Text, comment.CreatedAt, new PublicAuthor(author.Id, author.Profile.DisplayName, author.Profile.AvatarUrl),
                            db.CommentLikes.Count(like => like.CommentId == comment.Id), likes.Count, likes.Last);
            return await query.Skip(offset).Take(limit).ToArrayAsync(cancellationToken);
        });
}
