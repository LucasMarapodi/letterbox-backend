using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;

namespace Tcc.Infrastructure.Opinions;

public sealed class InMemoryReviewLikes : IReviewLikes
{
    private readonly Lock gate = new();
    private readonly Dictionary<(int User, int Review), ReviewLike> likes = [];
    public Task SetAsync(int userId, int reviewId, DateTimeOffset now, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock(gate) likes.TryAdd((userId,reviewId), new(userId, reviewId, now)); return Task.CompletedTask; }
    public Task RemoveAsync(int userId, int reviewId, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock(gate) likes.Remove((userId,reviewId)); return Task.CompletedTask; }
    public Task<bool> HasAsync(int userId, int reviewId, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock(gate) return Task.FromResult(likes.ContainsKey((userId,reviewId))); }
    public Task<IReadOnlyDictionary<int,int>> CountsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock(gate) return Task.FromResult<IReadOnlyDictionary<int,int>>(likes.Values.Where(like => ids.Contains(like.ReviewId)).GroupBy(like => like.ReviewId).ToDictionary(group => group.Key, group => group.Count())); }
}
