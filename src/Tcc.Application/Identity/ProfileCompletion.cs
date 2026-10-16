using Tcc.Application.Catalog;
using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;

public sealed record ProfileCompletionDetails(string? Country, string? State, string Bio, string? AvatarUrl, IReadOnlyList<int> FavoriteGameIds)
{
    public int FavoriteGameCount => FavoriteGameIds.Count;
    public bool IsComplete => ProfileCompletionPolicy.IsComplete(Country, State, Bio, FavoriteGameIds);
}
public sealed record ProfileCompletionChange(ProfileChange Profile, IReadOnlyList<int> FavoriteGameIds);
public interface IProfileCompletionStore
{
    Task<ProfileCompletionDetails?> ReadAsync(int userId, CancellationToken ct);
    Task<ProfileCompletionDetails?> SaveAsync(int userId, ProfileCompletionChange change, DateTimeOffset now, CancellationToken ct);
}
public sealed class ProfileCompletion(IProfileCompletionStore store, ICurrentSession current, SessionAccess access,
    IUserAccounts accounts, IAsyncGameCatalog catalog, TimeProvider clock)
{
    public async Task<ProfileCompletionDetails> Get(CancellationToken ct = default) =>
        await store.ReadAsync((await access.Require(current, ct)).UserId, ct) ?? throw new IdentityFailure(IdentityFailureKind.NotFound);

    public async Task<ProfileCompletionDetails> Set(string? country, string? state, string? bio, bool avatarSpecified,
        string? avatarUrl, IReadOnlyList<int>? favoriteGameIds, CancellationToken ct = default)
    {
        var userId = (await access.Require(current, ct)).UserId;
        if (string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(bio)
            || favoriteGameIds is null || favoriteGameIds.Any(id => id <= 0))
            throw new DomainValidationException("Informe localização, descrição e pelo menos três jogos distintos.");
        var ids = favoriteGameIds.Distinct().Order().ToArray();
        if (ids.Length < 3) throw new DomainValidationException("Selecione pelo menos três jogos distintos.");
        var change = new ProfileChange(null, bio.Trim(), avatarSpecified, avatarUrl, true, country.Trim(), true, state.Trim());
        var account = await accounts.FindByIdAsync(userId, ct) ?? throw new IdentityFailure(IdentityFailureKind.NotFound);
        // Domain validation and catalog I/O finish before the write transaction starts.
        _ = account.Profile.Apply(change, clock.GetUtcNow());
        foreach (var batch in ids.Chunk(500))
            if ((await catalog.ResolveAsync(batch, ct)).Count != batch.Length) throw new ResourceFailure(ResourceFailureKind.NotFound);
        return await store.SaveAsync(userId, new(change, ids), clock.GetUtcNow(), ct) ?? throw new IdentityFailure(IdentityFailureKind.NotFound);
    }
}
