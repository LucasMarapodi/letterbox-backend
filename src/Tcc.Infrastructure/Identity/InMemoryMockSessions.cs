using System.Collections.Concurrent;
using System.Security.Cryptography;
using Tcc.Application.Identity;

namespace Tcc.Infrastructure.Identity;

public sealed class InMemoryMockSessions : IMockSessions
{
    private readonly ConcurrentDictionary<string, MockSession> sessions = new(StringComparer.Ordinal);

    public MockSession Create(string userId, DateTimeOffset expiresAt)
    {
        while (true)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var session = new MockSession(token, userId, expiresAt);
            if (sessions.TryAdd(token, session)) return session;
        }
    }

    public MockSession? Find(string token) => sessions.GetValueOrDefault(token);
    public void Revoke(string token) => sessions.TryRemove(token, out _);
}
