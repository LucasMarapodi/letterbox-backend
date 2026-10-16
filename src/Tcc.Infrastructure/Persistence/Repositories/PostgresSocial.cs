using System.Data;
using Microsoft.EntityFrameworkCore;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;
using Tcc.Domain.Social;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresFavorites(Lazy<TccDbContext> context) : IFavorites
{
    public Task<Favorite> SetAsync(int userId, FavoriteKind kind, int targetId, DateTimeOffset now, CancellationToken ct = default) => OpinionStorage.Run(async () =>
    {
        var db = context.Value;
        var timestamp = now.ToUniversalTime();
        if (kind == FavoriteKind.Game)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await ProfileWriteLock.Acquire(db, userId, ct);
            var rows = await db.GameFavorites.FromSqlInterpolated($"""
                INSERT INTO jogos_favoritos(usuario_id,jogo_id,criado_em) VALUES({userId},{targetId},{timestamp})
                ON CONFLICT (usuario_id,jogo_id) DO UPDATE SET usuario_id=EXCLUDED.usuario_id RETURNING *
                """).AsNoTracking().ToArrayAsync(ct);
            await tx.CommitAsync(ct);
            return new Favorite(rows.Single().Id, userId, kind, targetId);
        }
        var reviews = await db.ReviewFavorites.FromSqlInterpolated($"""
            INSERT INTO reviews_favoritas(usuario_id,review_id,criado_em) VALUES({userId},{targetId},{timestamp})
            ON CONFLICT (usuario_id,review_id) DO UPDATE SET usuario_id=EXCLUDED.usuario_id RETURNING *
            """).AsNoTracking().ToArrayAsync(ct);
        return new Favorite(reviews.Single().Id, userId, kind, targetId);
    });
    public Task<Favorite?> FindAsync(int userId, FavoriteKind kind, int targetId, CancellationToken ct = default) => OpinionStorage.Run(async () =>
    {
        var db = context.Value;
        return kind == FavoriteKind.Game
            ? await db.GameFavorites.AsNoTracking().Where(item => item.UserId == userId && item.GameId == targetId)
                .Select(item => new Favorite(item.Id, item.UserId, kind, item.GameId)).SingleOrDefaultAsync(ct)
            : await db.ReviewFavorites.AsNoTracking().Where(item => item.UserId == userId && item.ReviewId == targetId)
                .Select(item => new Favorite(item.Id, item.UserId, kind, item.ReviewId)).SingleOrDefaultAsync(ct);
    });
    public Task RemoveAsync(int userId, FavoriteKind kind, int targetId, CancellationToken ct = default) => OpinionStorage.Run(async () =>
    {
        var db = context.Value;
        if (kind != FavoriteKind.Game) return await db.ReviewFavorites.Where(item => item.UserId == userId && item.ReviewId == targetId).ExecuteDeleteAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ProfileWriteLock.Acquire(db, userId, ct);
        var deleted = await db.GameFavorites.Where(item => item.UserId == userId && item.GameId == targetId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
        return deleted;
    });
    public Task<FavoriteSnapshot> SnapshotAsync(int userId, CancellationToken ct = default) => OpinionStorage.Run(async () =>
    {
        var db = context.Value;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var games = await db.GameFavorites.AsNoTracking().Where(item => item.UserId == userId).OrderBy(item => item.GameId).Select(item => item.GameId).ToArrayAsync(ct);
        var reviews = await (from favorite in db.ReviewFavorites.AsNoTracking()
                             join review in db.Reviews.AsNoTracking() on favorite.ReviewId equals review.Id
                             where favorite.UserId == userId
                             orderby favorite.ReviewId
                             select review).ToArrayAsync(ct);
        await tx.CommitAsync(ct);
        return new FavoriteSnapshot(games, reviews);
    });
}
public sealed class PostgresFollows(Lazy<TccDbContext> context) : IFollows
{
    public Task AddAsync(Follow follow, CancellationToken ct = default) => OpinionStorage.Run(async () =>
        await context.Value.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO seguimentos_usuarios(seguidor_id,seguido_id,criado_em) VALUES({follow.FollowerId},{follow.FollowedId},{follow.CreatedAt}) ON CONFLICT (seguidor_id,seguido_id) DO NOTHING", ct));
    public Task RemoveAsync(int followerId, int followedId, CancellationToken ct = default) => OpinionStorage.Run(async () =>
        await context.Value.Follows.Where(item => item.FollowerId == followerId && item.FollowedId == followedId).ExecuteDeleteAsync(ct));
    public Task<bool> ContainsAsync(int followerId, int followedId, CancellationToken ct = default) => OpinionStorage.Run(() =>
        context.Value.Follows.AsNoTracking().AnyAsync(item => item.FollowerId == followerId && item.FollowedId == followedId, ct));
    public Task<IReadOnlyList<int>> FollowersAsync(int userId, CancellationToken ct = default) => OpinionStorage.Run<IReadOnlyList<int>>(async () =>
        await context.Value.Follows.AsNoTracking().Where(item => item.FollowedId == userId).OrderBy(item => item.FollowerId).Select(item => item.FollowerId).ToArrayAsync(ct));
    public Task<IReadOnlyList<int>> FollowingAsync(int userId, CancellationToken ct = default) => OpinionStorage.Run<IReadOnlyList<int>>(async () =>
        await context.Value.Follows.AsNoTracking().Where(item => item.FollowerId == userId).OrderBy(item => item.FollowedId).Select(item => item.FollowedId).ToArrayAsync(ct));
    public Task<IReadOnlyList<Review>> FeedAsync(int userId, CancellationToken ct = default) => OpinionStorage.Run<IReadOnlyList<Review>>(async () =>
        await context.Value.Reviews.AsNoTracking().Where(review => context.Value.Follows.Any(follow => follow.FollowerId == userId && follow.FollowedId == review.UserId))
            .OrderByDescending(review => review.CreatedAt).ThenBy(review => review.Id).ToArrayAsync(ct));
}
