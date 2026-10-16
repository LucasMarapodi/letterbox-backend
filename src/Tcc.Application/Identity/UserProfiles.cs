using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;
public sealed class GetUserProfile(IUserAccounts accounts)
{
    public Task<UserAccount> Execute(int id, CancellationToken cancellationToken = default) => Get(id, cancellationToken);
    private async Task<UserAccount> Get(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new DomainValidationException("ID de usuário inválido.");
        return await accounts.FindByIdAsync(id, cancellationToken) ?? throw new IdentityFailure(IdentityFailureKind.NotFound);
    }
}
public sealed class EditUserProfile(ProfileCompletion completion)
{
    public Task<ProfileSaveResult> Execute(ProfileChange change, IReadOnlyList<int>? favoriteGameIds, CancellationToken cancellationToken = default) =>
        completion.Patch(change, favoriteGameIds, cancellationToken);
}
