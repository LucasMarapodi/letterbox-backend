using Tcc.Application.Catalog;
using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;

public sealed record ProfileCompletionDetails(string? Country, string? State, string Bio, string? AvatarUrl, IReadOnlyList<int> FavoriteGameIds)
{
    public int FavoriteGameCount => FavoriteGameIds.Count;
    public bool IsComplete => ProfileCompletionPolicy.IsComplete(Country, State, Bio, FavoriteGameIds);
}
public sealed record ProfileCompletionChange(ProfileChange Profile, IReadOnlyList<int>? FavoriteGameIds);
public sealed record ProfileSaveResult(UserAccount User, ProfileCompletionDetails Completion);
public interface IProfileCompletionStore
{
    Task<ProfileCompletionDetails?> ReadAsync(int userId, CancellationToken ct);
    Task<ProfileSaveResult?> SaveAsync(int userId, ProfileCompletionChange change, DateTimeOffset now, CancellationToken ct);
}
public sealed class ProfileCompletion(IProfileCompletionStore store, ICurrentSession current, SessionAccess access,
    IUserAccounts accounts, IAsyncGameCatalog catalog, TimeProvider clock)
{
    public async Task<ProfileCompletionDetails> Get(CancellationToken ct = default) =>
        await store.ReadAsync((await access.Require(current, ct)).UserId, ct) ?? throw new IdentityFailure(IdentityFailureKind.NotFound);

    public async Task<ProfileCompletionDetails> Set(string? country, string? state, string? bio, bool avatarSpecified,
        string? avatarUrl, IReadOnlyList<int>? favoriteGameIds, CancellationToken ct = default)
    {
        if (favoriteGameIds is null) throw new DomainValidationException("Informe os jogos favoritos.");
        var saved = await Patch(new(null, bio ?? "", avatarSpecified, avatarUrl, true, country, true, state), favoriteGameIds, ct);
        return saved.Completion;
    }

    public async Task<ProfileSaveResult> Patch(ProfileChange change, IReadOnlyList<int>? favoriteGameIds, CancellationToken ct = default)
    {
        var userId = (await access.Require(current, ct)).UserId;
        if (favoriteGameIds?.Any(id => id <= 0) == true) throw new DomainValidationException("IDs de jogos inválidos.");
        var ids = favoriteGameIds?.Distinct().Order().ToArray();
        var account = await accounts.FindByIdAsync(userId, ct) ?? throw new IdentityFailure(IdentityFailureKind.NotFound);
        // Domain validation and catalog I/O finish before the write transaction starts.
        _ = account.Profile.Apply(change, clock.GetUtcNow());
        foreach (var batch in (ids ?? []).Chunk(500))
            if ((await catalog.ResolveAsync(batch, ct)).Count != batch.Length) throw new ResourceFailure(ResourceFailureKind.NotFound);
        return await store.SaveAsync(userId, new(change, ids), clock.GetUtcNow(), ct) ?? throw new IdentityFailure(IdentityFailureKind.NotFound);
    }
}
