using Tcc.Application.Social;
using Tcc.Domain.Social;

namespace Tcc.Infrastructure.Social;

public sealed class InMemoryFollows : IFollows
{
    private readonly Lock gate = new();
    private readonly Dictionary<(string Follower, string Followed), Follow> follows = [];
    public void Add(Follow follow) { lock (gate) follows.TryAdd((follow.FollowerId, follow.FollowedId), follow); }
    public void Remove(string followerId, string followedId) { lock (gate) follows.Remove((followerId, followedId)); }
    public bool Contains(string followerId, string followedId) { lock (gate) return follows.ContainsKey((followerId, followedId)); }
    public IReadOnlyList<string> Followers(string userId)
    {
        lock (gate) return follows.Values.Where(follow => follow.FollowedId == userId).Select(follow => follow.FollowerId).ToArray();
    }
    public IReadOnlyList<string> Following(string userId)
    {
        lock (gate) return follows.Values.Where(follow => follow.FollowerId == userId).Select(follow => follow.FollowedId).ToArray();
    }
}
