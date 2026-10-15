using Tcc.Application.Identity;
using Tcc.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresUserSessions(Lazy<TccDbContext> context) : IUserSessions
{
    public Task<SessionTicket> CreateAsync(int userId, DateTimeOffset createdAt, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => Run(async db =>
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new UserSession(userId, Hash(token), createdAt, expiresAt);
        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return new SessionTicket(token, userId, expiresAt, session.Id);
    });
    public Task<SessionTicket?> FindAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!ValidToken(token)) return Task.FromResult<SessionTicket?>(null);
        return Run<SessionTicket?>(async db =>
        {
            var hash = Hash(token);
            var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(item => item.TokenHash == hash && item.RevokedAt == null, cancellationToken);
            return session is null ? null : new(token, session.UserId, session.ExpiresAt, session.Id);
        });
    }
    public async Task RevokeAsync(string token, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (!ValidToken(token)) return;
        await Run(async db =>
        {
            var hash = Hash(token);
            return await db.Sessions.Where(item => item.TokenHash == hash && item.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, now), cancellationToken);
        });
    }
    private static bool ValidToken(string token) => token.Length == 64 && token.All(char.IsAsciiHexDigit);
    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private async Task<T> Run<T>(Func<TccDbContext, Task<T>> action)
    {
        try { return await action(context.Value); }
        catch (Exception failure) when (failure is NpgsqlException or DbUpdateException or DatabaseConfigurationException
            or InvalidOperationException { InnerException: NpgsqlException })
        { throw new DatabaseUnavailableException(); }
    }
}
