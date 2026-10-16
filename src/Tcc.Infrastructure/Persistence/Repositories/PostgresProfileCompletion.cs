using System.Data;
using Microsoft.EntityFrameworkCore;
using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Social;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Persistence.Repositories;

internal static class ProfileWriteLock
{
    public static Task Acquire(TccDbContext db, int userId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM usuarios WHERE id={userId} FOR UPDATE", ct);
}

public sealed class PostgresProfileCompletion(Lazy<TccDbContext> context) : IProfileCompletionStore
{
    public Task<ProfileCompletionDetails?> ReadAsync(int userId, CancellationToken ct) => OpinionStorage.Run<ProfileCompletionDetails?>(async () =>
    {
        var db = context.Value;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var profile = await db.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => user.Profile).SingleOrDefaultAsync(ct);
        if (profile is null) return null;
        var ids = await (from favorite in db.GameFavorites.AsNoTracking()
                         join game in db.Games.AsNoTracking() on favorite.GameId equals game.Id
                         where favorite.UserId == userId orderby favorite.GameId select favorite.GameId).ToArrayAsync(ct);
        await tx.CommitAsync(ct);
        return new(profile.Country, profile.State, profile.Bio, profile.AvatarUrl, ids);
    });

    public Task<ProfileCompletionDetails?> SaveAsync(int userId, ProfileCompletionChange change, DateTimeOffset now, CancellationToken ct) => OpinionStorage.Run<ProfileCompletionDetails?>(async () =>
    {
        var db = context.Value;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ProfileWriteLock.Acquire(db, userId, ct);
        var account = await db.Users.AsTracking().SingleOrDefaultAsync(user => user.Id == userId, ct);
        if (account is null) return null;
        var ids = change.FavoriteGameIds.ToArray();
        // Recheck SQL existence even if the catalog had cached these IDs before the transaction.
        if (await db.Games.CountAsync(game => ids.Contains(game.Id), ct) != ids.Length) throw new ResourceFailure(ResourceFailureKind.NotFound);
        var updated = account.Profile.Apply(change.Profile, now);
        if (!ProfileCompletionPolicy.IsComplete(updated.Country, updated.State, updated.Bio, ids))
            throw new Tcc.Domain.Identity.DomainValidationException("Dados de conclusão inválidos.");
        if (updated.Country != account.Profile.Country || updated.State != account.Profile.State || updated.Bio != account.Profile.Bio || updated.AvatarUrl != account.Profile.AvatarUrl)
        {
            db.Entry(account.Profile).CurrentValues.SetValues(updated);
            db.Entry(account).Property(user => user.UpdatedAt).CurrentValue = now;
        }
        var existing = await db.GameFavorites.AsNoTracking().Where(item => item.UserId == userId).Select(item => item.GameId).ToArrayAsync(ct);
        await db.GameFavorites.Where(item => item.UserId == userId && !ids.Contains(item.GameId)).ExecuteDeleteAsync(ct);
        foreach (var id in ids.Except(existing)) db.GameFavorites.Add(new GameFavorite(0, userId, id, now));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(updated.Country, updated.State, updated.Bio, updated.AvatarUrl, ids);
    });
}
