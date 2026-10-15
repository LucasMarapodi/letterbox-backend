using Tcc.Application.Identity;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresUserSessions : IMockSessions
{
    public Task<MockSession> CreateAsync(int userId, DateTimeOffset createdAt, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => throw new DatabaseUnavailableException();
    public Task<MockSession?> FindAsync(string token, CancellationToken cancellationToken = default) => Task.FromResult<MockSession?>(null);
    public Task RevokeAsync(string token, DateTimeOffset now, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
