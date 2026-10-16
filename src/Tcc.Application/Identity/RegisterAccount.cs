using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;
public sealed class RegisterAccount(IUserAccounts accounts, IPasswordHasher passwords, TimeProvider clock)
{
    public async Task<UserAccount> Execute(string? name, string? email, string? password, CancellationToken cancellationToken = default)
    {
        var address = Email.Create(email);
        var now = clock.GetUtcNow();
        var profile = UserProfile.Create(name, updatedAt: now);
        if (string.IsNullOrWhiteSpace(password)) throw new DomainValidationException("Senha obrigatória.");
        var account = new UserAccount(0, "", address, passwords.Hash(password), profile, now);
        return await accounts.AddAsync(account, cancellationToken) ?? throw new IdentityFailure(IdentityFailureKind.Conflict);
    }
}
