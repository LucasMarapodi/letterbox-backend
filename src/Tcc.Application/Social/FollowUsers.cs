using Tcc.Application.Identity;
using Tcc.Domain.Identity;
using Tcc.Domain.Social;

namespace Tcc.Application.Social;
public interface IFollows
{
    void Add(Follow follow);
    void Remove(int followerId, int followedId);
    bool Contains(int followerId, int followedId);
    IReadOnlyList<int> Followers(int userId);
    IReadOnlyList<int> Following(int userId);
}
public sealed class FollowUsers(IFollows follows, GetUserProfile users, IUserAccounts accounts, ICurrentSession current, SessionAccess access)
{
    public async Task Set(int targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        await users.Execute(targetId, cancellationToken);
        follows.Add(Follow.Create(userId, targetId));
    }
    public async Task Remove(int targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        await users.Execute(targetId, cancellationToken);
        follows.Remove(userId, targetId);
    }
    public async Task<bool> IsFollowing(int targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        await users.Execute(targetId, cancellationToken);
        return follows.Contains(userId, targetId);
    }
    public async Task<IReadOnlyList<UserAccount>> Followers(int userId, CancellationToken cancellationToken = default)
    {
        await users.Execute(userId, cancellationToken);
        return await Project(follows.Followers(userId), cancellationToken);
    }
    public async Task<IReadOnlyList<UserAccount>> Following(int userId, CancellationToken cancellationToken = default)
    {
        await users.Execute(userId, cancellationToken);
        return await Project(follows.Following(userId), cancellationToken);
    }
    private async Task<IReadOnlyList<UserAccount>> Project(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var found = await accounts.FindManyAsync(ids, cancellationToken);
        return ids.Order().Where(found.ContainsKey).Select(id => found[id]).ToArray();
    }
}
