using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;

public sealed record AccountLogin(MockSession Session, UserAccount Account);

public sealed class LoginAccount(IUserAccounts accounts, IPasswordHasher passwords, IMockSessions sessions,
    TimeProvider clock, SessionSettings settings)
{
    public AccountLogin Execute(string? email, string? password)
    {
        Email address;
        try { address = Email.Create(email); }
        catch (DomainValidationException) { throw new IdentityFailure(IdentityFailureKind.Unauthorized); }
        var account = accounts.FindByEmail(address);
        if (account is null || string.IsNullOrWhiteSpace(password) || !passwords.Verify(password, account.PasswordHash))
            throw new IdentityFailure(IdentityFailureKind.Unauthorized);
        var session = sessions.Create(account.Id, clock.GetUtcNow().Add(settings.Lifetime));
        return new AccountLogin(session, account);
    }
}
