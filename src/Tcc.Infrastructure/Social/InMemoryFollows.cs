using Tcc.Application.Social;
using Tcc.Domain.Social;

namespace Tcc.Infrastructure.Social;

public sealed class InMemoryFollows(InMemoryPublicationScope publications) : IFollows
{
    private readonly Lock gate = publications.Gate;
    private readonly Dictionary<(int Follower, int Followed), Follow> follows = [];
    public void Add(Follow follow) { lock (gate) follows.TryAdd((follow.FollowerId, follow.FollowedId), follow); }
    public void Remove(int followerId, int followedId) { lock (gate) follows.Remove((followerId, followedId)); }
    public bool Contains(int followerId, int followedId) { lock (gate) return follows.ContainsKey((followerId, followedId)); }
    public IReadOnlyList<int> Followers(int userId)
    {
        lock (gate) return follows.Values.Where(follow => follow.FollowedId == userId).Select(follow => follow.FollowerId).ToArray();
    }
    public IReadOnlyList<int> Following(int userId)
    {
        lock (gate) return follows.Values.Where(follow => follow.FollowerId == userId).Select(follow => follow.FollowedId).ToArray();
    }
}
