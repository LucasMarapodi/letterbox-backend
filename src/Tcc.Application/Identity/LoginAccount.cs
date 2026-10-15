using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;
public sealed record AccountLogin(SessionTicket Session, UserAccount Account);
public sealed class LoginAccount(IUserAccounts accounts, IPasswordHasher passwords, IUserSessions sessions, TimeProvider clock, SessionSettings settings)
{
    public async Task<AccountLogin> Execute(string? email, string? password, CancellationToken cancellationToken = default)
    {
        Email address;
        try { address = Email.Create(email); }
        catch (DomainValidationException) { throw new IdentityFailure(IdentityFailureKind.Unauthorized); }
        var account = await accounts.FindByEmailAsync(address, cancellationToken);
        if (account is null || string.IsNullOrWhiteSpace(password) || !passwords.Verify(password, account.PasswordHash))
            throw new IdentityFailure(IdentityFailureKind.Unauthorized);
        var now = clock.GetUtcNow();
        var session = await sessions.CreateAsync(account.Id, now, now.Add(settings.Lifetime), cancellationToken);
        return new(session, account);
    }
}
