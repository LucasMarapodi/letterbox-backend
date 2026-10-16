using Microsoft.EntityFrameworkCore;
using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresReviewLikes(Lazy<TccDbContext> context) : IReviewLikes
{
    public Task SetAsync(int userId, int reviewId, DateTimeOffset now, CancellationToken ct = default) => OpinionStorage.Run(async () =>
    {
        var like = new ReviewLike(userId, reviewId, now);
        return await context.Value.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO curtidas_reviews(usuario_id,review_id,criado_em) VALUES({like.UserId},{like.ReviewId},{like.CreatedAt}) ON CONFLICT (usuario_id,review_id) DO NOTHING", ct);
    });
    public Task RemoveAsync(int userId, int reviewId, CancellationToken ct = default) => OpinionStorage.Run(async () =>
        await context.Value.ReviewLikes.Where(item => item.UserId == userId && item.ReviewId == reviewId).ExecuteDeleteAsync(ct));
    public Task<bool> HasAsync(int userId, int reviewId, CancellationToken ct = default) => OpinionStorage.Run(() =>
        context.Value.ReviewLikes.AsNoTracking().AnyAsync(item => item.UserId == userId && item.ReviewId == reviewId, ct));
    public Task<IReadOnlyDictionary<int, int>> CountsAsync(IReadOnlyList<int> reviewIds, CancellationToken ct = default)
    {
        if (reviewIds.Count == 0) return Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int,int>());
        return OpinionStorage.Run<IReadOnlyDictionary<int, int>>(async () => await context.Value.ReviewLikes.AsNoTracking()
            .Where(item => reviewIds.Contains(item.ReviewId)).GroupBy(item => item.ReviewId)
            .Select(group => new { ReviewId = group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.ReviewId, item => item.Count, ct));
    }
}
