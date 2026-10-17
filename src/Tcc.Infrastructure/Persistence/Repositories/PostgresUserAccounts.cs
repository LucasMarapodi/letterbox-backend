using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tcc.Application.Identity;
using Tcc.Application;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresUserAccounts(Lazy<TccDbContext> context) : IUserAccounts
{
    public Task<UserAccount?> AddAsync(UserAccount account, CancellationToken cancellationToken = default) => Run<UserAccount?>(async db =>
    {
        db.Users.Add(account);
        try { await db.SaveChangesAsync(cancellationToken); return account; }
        catch (DbUpdateException failure) when (failure.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.ChangeTracker.Clear(); return null; }
    });
    public Task<UserAccount?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        Run(db => db.Users.AsNoTracking().SingleOrDefaultAsync(account => account.Email == email, cancellationToken));
    public Task<UserAccount?> FindByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Run(db => db.Users.AsNoTracking().SingleOrDefaultAsync(account => account.Id == id, cancellationToken));
    public Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Run(db => db.Users.AsNoTracking().SingleOrDefaultAsync(account => account.Username == username, cancellationToken));
    public Task<IReadOnlyDictionary<int, UserAccount>> FindManyAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0 ? Task.FromResult<IReadOnlyDictionary<int, UserAccount>>(new Dictionary<int, UserAccount>()) : Run<IReadOnlyDictionary<int, UserAccount>>(async db =>
        {
            return await db.Users.AsNoTracking().Where(account => ids.Contains(account.Id)).ToDictionaryAsync(account => account.Id, cancellationToken);
        });
    public Task<UserAccount?> UpdateProfileAsync(int id, ProfileChange change, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Run<UserAccount?>(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await ProfileWriteLock.Acquire(db, id, cancellationToken);
            var account = await db.Users.AsTracking().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
            if (account is null) return null;
            var updated = account.UpdateProfile(change, now);
            db.Entry(account.Profile).CurrentValues.SetValues(updated.Profile);
            db.Entry(account).Property(item => item.UpdatedAt).CurrentValue = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return account;
        });
    private async Task<T> Run<T>(Func<TccDbContext, Task<T>> action)
    {
        try { return await action(context.Value); }
        catch (Exception failure) when (failure is NpgsqlException or DbUpdateException or DatabaseConfigurationException
            or InvalidOperationException { InnerException: NpgsqlException })
        { throw new DatabaseUnavailableException(); }
    }
}
