using Tcc.Application.Identity;
using Tcc.Domain.Identity;
using Tcc.Domain.Social;

namespace Tcc.Application.Social;
public interface IFollows
{
    Task AddAsync(Follow follow, CancellationToken ct = default);
    Task RemoveAsync(int followerId, int followedId, CancellationToken ct = default);
    Task<bool> ContainsAsync(int followerId, int followedId, CancellationToken ct = default);
    Task<IReadOnlyList<int>> FollowersAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<int>> FollowingAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<Tcc.Domain.Opinions.Review>> FeedAsync(int userId, CancellationToken ct = default);
}
public sealed class FollowUsers(IFollows follows, GetUserProfile users, IUserAccounts accounts, ICurrentSession current, SessionAccess access, TimeProvider clock)
{
    public async Task Set(int targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        await users.Execute(targetId, cancellationToken);
        await access.Require(current, cancellationToken);
        await follows.AddAsync(Follow.Create(userId, targetId, clock.GetUtcNow()), cancellationToken);
    }
    public async Task Remove(int targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        await users.Execute(targetId, cancellationToken);
        await access.Require(current, cancellationToken);
        await follows.RemoveAsync(userId, targetId, cancellationToken);
    }
    public async Task<bool> IsFollowing(int targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        await users.Execute(targetId, cancellationToken);
        return await follows.ContainsAsync(userId, targetId, cancellationToken);
    }
    public async Task<IReadOnlyList<UserAccount>> Followers(int userId, CancellationToken cancellationToken = default)
    {
        await users.Execute(userId, cancellationToken);
        return await Project(await follows.FollowersAsync(userId, cancellationToken), cancellationToken);
    }
    public async Task<IReadOnlyList<UserAccount>> Following(int userId, CancellationToken cancellationToken = default)
    {
        await users.Execute(userId, cancellationToken);
        return await Project(await follows.FollowingAsync(userId, cancellationToken), cancellationToken);
    }
    private async Task<IReadOnlyList<UserAccount>> Project(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var result = new List<UserAccount>();
        foreach (var chunk in ids.Order().Chunk(500))
        {
            var found = await accounts.FindManyAsync(chunk, cancellationToken);
            result.AddRange(chunk.Where(found.ContainsKey).Select(id => found[id]));
        }
        return result;
    }
}
