namespace Tcc.Application.Identity;
public sealed record SessionTicket(string Token, int UserId, DateTimeOffset ExpiresAt, int SessionId = 0)
{
    public override string ToString() => "SessionTicket [redacted]";
}
public interface IUserSessions
{
    Task<SessionTicket> CreateAsync(int userId, DateTimeOffset createdAt, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task<SessionTicket?> FindAsync(string token, CancellationToken cancellationToken = default);
    Task RevokeAsync(string token, DateTimeOffset now, CancellationToken cancellationToken = default);
}
public sealed record SessionSettings(TimeSpan Lifetime);
