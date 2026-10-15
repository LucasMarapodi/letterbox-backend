namespace Tcc.Application.Identity;
public interface ICurrentSession { SessionTicket? Session { get; } }
public sealed class SessionAccess(IUserSessions sessions, IUserAccounts accounts, TimeProvider clock)
{
    public async Task<SessionTicket?> Resolve(string token, CancellationToken cancellationToken = default)
    {
        var session = await sessions.FindAsync(token, cancellationToken);
        if (session is null) return null;
        if (clock.GetUtcNow() >= session.ExpiresAt)
        {
            await sessions.RevokeAsync(token, clock.GetUtcNow(), cancellationToken);
            return null;
        }
        return await accounts.FindByIdAsync(session.UserId, cancellationToken) is not null ? session : null;
    }
    public async Task<SessionTicket> Require(ICurrentSession current, CancellationToken cancellationToken = default) =>
        current.Session is { } session ? await Resolve(session.Token, cancellationToken) ?? throw new IdentityFailure(IdentityFailureKind.Unauthorized)
            : throw new IdentityFailure(IdentityFailureKind.Unauthorized);
}
public sealed class CurrentAccount(ICurrentSession current, SessionAccess access, IUserAccounts accounts)
{
    public async Task<Tcc.Domain.Identity.UserAccount> Execute(CancellationToken cancellationToken = default) =>
        await accounts.FindByIdAsync((await access.Require(current, cancellationToken)).UserId, cancellationToken)
            ?? throw new IdentityFailure(IdentityFailureKind.Unauthorized);
}
public sealed class LogoutAccount(ICurrentSession current, SessionAccess access, IUserSessions sessions, TimeProvider clock)
{
    public async Task Execute(CancellationToken cancellationToken = default) =>
        await sessions.RevokeAsync((await access.Require(current, cancellationToken)).Token, clock.GetUtcNow(), cancellationToken);
}
