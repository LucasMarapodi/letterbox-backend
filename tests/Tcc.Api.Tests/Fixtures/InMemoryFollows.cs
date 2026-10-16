using Tcc.Application.Social;
using Tcc.Application.Opinions;
using Tcc.Domain.Social;
using Tcc.Domain.Opinions;
namespace Tcc.Infrastructure.Social;

public sealed class InMemoryFollows(InMemoryPublicationScope publications, IReviews reviews) : IFollows
{
    private readonly Lock gate = publications.Gate;
    private readonly Dictionary<(int Follower, int Followed), Follow> follows = [];
    public Task AddAsync(Follow follow, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (gate) follows.TryAdd((follow.FollowerId, follow.FollowedId), follow); return Task.CompletedTask; }
    public Task RemoveAsync(int followerId, int followedId, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (gate) follows.Remove((followerId, followedId)); return Task.CompletedTask; }
    public Task<bool> ContainsAsync(int followerId, int followedId, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult(follows.ContainsKey((followerId, followedId))); }
    public Task<IReadOnlyList<int>> FollowersAsync(int userId, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult<IReadOnlyList<int>>(follows.Values.Where(item => item.FollowedId == userId).Select(item => item.FollowerId).ToArray()); }
    public Task<IReadOnlyList<int>> FollowingAsync(int userId, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult<IReadOnlyList<int>>(follows.Values.Where(item => item.FollowerId == userId).Select(item => item.FollowedId).ToArray()); }
    public async Task<IReadOnlyList<Review>> FeedAsync(int userId, CancellationToken ct = default) => await reviews.QueryAsync(new(Authors: await FollowingAsync(userId, ct)), ct);
}
