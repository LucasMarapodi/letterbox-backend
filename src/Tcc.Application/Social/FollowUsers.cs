using Tcc.Application.Identity;
using Tcc.Domain.Identity;
using Tcc.Domain.Social;

namespace Tcc.Application.Social;

public interface IFollows
{
    void Add(Follow follow);
    void Remove(string followerId, string followedId);
    bool Contains(string followerId, string followedId);
    IReadOnlyList<string> Followers(string userId);
    IReadOnlyList<string> Following(string userId);
}

public sealed class FollowUsers(IFollows follows, GetUserProfile users, ICurrentSession current, SessionAccess access)
{
    public void Set(string targetId)
    {
        var userId = access.Require(current).UserId;
        users.Execute(targetId);
        follows.Add(Follow.Create(userId, targetId));
    }
    public void Remove(string targetId)
    {
        var userId = access.Require(current).UserId;
        users.Execute(targetId);
        follows.Remove(userId, targetId);
    }
    public bool IsFollowing(string targetId)
    {
        var userId = access.Require(current).UserId;
        users.Execute(targetId);
        return follows.Contains(userId, targetId);
    }
    public IReadOnlyList<UserAccount> Followers(string userId)
    {
        users.Execute(userId);
        return follows.Followers(userId).Order(StringComparer.Ordinal).Select(users.Execute).ToArray();
    }
    public IReadOnlyList<UserAccount> Following(string userId)
    {
        users.Execute(userId);
        return follows.Following(userId).Order(StringComparer.Ordinal).Select(users.Execute).ToArray();
    }
}
