using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;

public interface ICurrentSession
{
    MockSession? Session { get; }
}

public sealed class SessionAccess(IMockSessions sessions, IUserAccounts accounts, TimeProvider clock)
{
    public MockSession? Resolve(string token)
    {
        var session = sessions.Find(token);
        if (session is null) return null;
        if (clock.GetUtcNow() >= session.ExpiresAt)
        {
            sessions.Revoke(token);
            return null;
        }
        return accounts.FindById(session.UserId) is not null ? session : null;
    }

    public MockSession Require(ICurrentSession current)
    {
        var session = current.Session;
        return session is null ? throw new IdentityFailure(IdentityFailureKind.Unauthorized)
            : Resolve(session.Token) ?? throw new IdentityFailure(IdentityFailureKind.Unauthorized);
    }
}

public sealed class CurrentAccount(ICurrentSession current, SessionAccess access, IUserAccounts accounts)
{
    public UserAccount Execute() => accounts.FindById(access.Require(current).UserId)
        ?? throw new IdentityFailure(IdentityFailureKind.Unauthorized);
}

public sealed class LogoutAccount(ICurrentSession current, SessionAccess access, IMockSessions sessions)
{
    public void Execute() => sessions.Revoke(access.Require(current).Token);
}
