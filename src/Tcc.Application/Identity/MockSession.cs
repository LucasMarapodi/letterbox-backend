namespace Tcc.Application.Identity;

public sealed record MockSession(string Token, string UserId, DateTimeOffset ExpiresAt);

public interface IMockSessions
{
    MockSession Create(string userId, DateTimeOffset expiresAt);
    MockSession? Find(string token);
    void Revoke(string token);
}

public sealed record SessionSettings(TimeSpan Lifetime);
