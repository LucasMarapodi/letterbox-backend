using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Application.Identity;
using Tcc.Domain.Identity;

namespace Tcc.Api.Tests.Fixtures;

public static class TestIdentityRegistration
{
    public static IServiceCollection AddTestIdentity(this IServiceCollection services)
    {
        services.AddSingleton<IUserAccounts, TestUserAccounts>();
        services.AddSingleton<IUserSessions, TestSessions>();
        return services;
    }
}

public sealed class TestUserAccounts : IUserAccounts
{
    private readonly object gate = new();
    private readonly Dictionary<int, UserAccount> accounts = [];
    private int next = 1;
    public TestUserAccounts(IPasswordHasher passwords)
    {
        accounts.Add(1, new(1, "ana", Email.Create("ana@example.com"), passwords.Hash("teste123"), UserProfile.Create("Ana", "Explorando novos mundos.")));
    }
    public Task<UserAccount?> AddAsync(UserAccount account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (accounts.Values.Any(item => item.Email == account.Email)) return Task.FromResult<UserAccount?>(null);
            var id = ++next;
            var saved = new UserAccount(id, "user-" + id, account.Email, account.PasswordHash, account.Profile, account.CreatedAt, account.UpdatedAt);
            accounts.Add(id, saved);
            return Task.FromResult<UserAccount?>(saved);
        }
    }
    public Task<UserAccount?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult(accounts.Values.SingleOrDefault(account => account.Email == email)); }
    public Task<UserAccount?> FindByIdAsync(int id, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult(accounts.GetValueOrDefault(id)); }
    public Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult(accounts.Values.SingleOrDefault(account => account.Username == username)); }
    public Task<IReadOnlyDictionary<int, UserAccount>> FindManyAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult<IReadOnlyDictionary<int, UserAccount>>(ids.Distinct().Where(accounts.ContainsKey).ToDictionary(id => id, id => accounts[id])); }
    public Task<UserAccount?> UpdateProfileAsync(int id, ProfileChange change, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!accounts.TryGetValue(id, out var account)) return Task.FromResult<UserAccount?>(null);
            var updated = account.UpdateProfile(change, now); accounts[id] = updated;
            return Task.FromResult<UserAccount?>(updated);
        }
    }
}
public sealed class TestSessions : IUserSessions
{
    private readonly ConcurrentDictionary<string, SessionTicket> sessions = new(StringComparer.Ordinal);
    private int next;
    public Task<SessionTicket> CreateAsync(int userId, DateTimeOffset createdAt, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = new SessionTicket(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), userId, expiresAt, Interlocked.Increment(ref next));
        sessions[session.Token] = session;
        return Task.FromResult(session);
    }
    public Task<SessionTicket?> FindAsync(string token, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(sessions.GetValueOrDefault(token)); }
    public Task RevokeAsync(string token, DateTimeOffset now, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); sessions.TryRemove(token, out _); return Task.CompletedTask; }
}
