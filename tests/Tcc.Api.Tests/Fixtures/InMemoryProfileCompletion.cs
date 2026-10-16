using Tcc.Application.Identity;
using Tcc.Application.Social;
using Tcc.Domain.Social;

namespace Tcc.Api.Tests.Fixtures;

public sealed class InMemoryProfileCompletion(IUserAccounts accounts, IFavorites favorites) : IProfileCompletionStore
{
    private readonly SemaphoreSlim writers = new(1,1);
    public async Task<ProfileCompletionDetails?> ReadAsync(int userId, CancellationToken ct)
    {
        var account = await accounts.FindByIdAsync(userId, ct);
        if (account is null) return null;
        var ids = (await favorites.SnapshotAsync(userId, ct)).GameIds.Order().ToArray();
        return new(account.Profile.Country, account.Profile.State, account.Profile.Bio, account.Profile.AvatarUrl, ids);
    }
    public async Task<ProfileSaveResult?> SaveAsync(int userId, ProfileCompletionChange change, DateTimeOffset now, CancellationToken ct)
    {
        await writers.WaitAsync(ct);
        try
        {
            var account = await accounts.UpdateProfileAsync(userId, change.Profile, now, ct);
            if (account is null) return null;
            if (change.FavoriteGameIds is not null)
            {
                var current = (await favorites.SnapshotAsync(userId, ct)).GameIds;
                foreach (var id in current.Except(change.FavoriteGameIds)) await favorites.RemoveAsync(userId, FavoriteKind.Game, id, ct);
                foreach (var id in change.FavoriteGameIds.Except(current)) await favorites.SetAsync(userId, FavoriteKind.Game, id, now, ct);
            }
            return new(account, (await ReadAsync(userId, ct))!);
        }
        finally { writers.Release(); }
    }
}
