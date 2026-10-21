using Microsoft.EntityFrameworkCore;
using Tcc.Application.Opinions;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresReviewRanking(Lazy<TccDbContext> context) : IReviewRanking
{
    public Task<IReadOnlyList<LikedReviewDetails>> ReadAsync(DateTimeOffset from, DateTimeOffset to,
        int offset, int limit, CancellationToken cancellationToken = default) =>
        OpinionStorage.Run<IReadOnlyList<LikedReviewDetails>>(async () =>
        {
            var db = context.Value;
            var activity = db.ReviewLikes.AsNoTracking().Where(like => like.CreatedAt >= from && like.CreatedAt <= to)
                .GroupBy(like => like.ReviewId)
                .Select(group => new { ReviewId = group.Key, Count = group.Count(), Last = group.Max(like => like.CreatedAt) });
            var query = from likes in activity
                        join review in db.Reviews.AsNoTracking() on likes.ReviewId equals review.Id
                        join game in db.Games.AsNoTracking() on review.GameId equals game.Id
                        join author in db.Users.AsNoTracking() on review.UserId equals author.Id
                        orderby likes.Count descending, likes.Last descending, review.Id
                        select new LikedReviewDetails(new ReviewDetails(review,
                            new PublicAuthor(author.Id, author.Profile.DisplayName, author.Profile.AvatarUrl),
                            new PublicGame(game.Id, game.Title, game.CoverUrl),
                            db.ReviewLikes.Count(like => like.ReviewId == review.Id)), likes.Count, likes.Last);
            return await query.Skip(offset).Take(limit).ToArrayAsync(cancellationToken);
        });
}
