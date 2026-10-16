using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;

public sealed class RegisterAccount(IUserAccounts accounts, IPasswordHasher passwords)
{
    public UserAccount Execute(string? name, string? email, string? password)
    {
        var address = Email.Create(email);
        var profile = UserProfile.Create(name);
        if (string.IsNullOrWhiteSpace(password))
            throw new DomainValidationException("Senha obrigatória.");
        var id = Guid.NewGuid().ToString("N");
        var account = new UserAccount(id, $"user-{id}", address, passwords.Hash(password), profile);
        if (!accounts.TryAdd(account)) throw new IdentityFailure(IdentityFailureKind.Conflict);
        return account;
    }
}
