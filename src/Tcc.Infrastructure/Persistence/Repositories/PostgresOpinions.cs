using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;

namespace Tcc.Infrastructure.Persistence.Repositories;

internal static class OpinionStorage
{
    public static async Task<T> Run<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (Exception failure) when (failure is NpgsqlException or DbUpdateException or DatabaseConfigurationException
            or InvalidOperationException { InnerException: NpgsqlException }) { throw new DatabaseUnavailableException(); }
    }
}
public sealed class PostgresReviews(Lazy<TccDbContext> context) : IReviews
{
    public Task<Review?> AddAsync(Review review, CancellationToken ct = default, Func<Review, CancellationToken, Task>? publish = null) => OpinionStorage.Run<Review?>(async () =>
    {
        var db = context.Value;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Reviews.Add(review);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException failure) when (failure.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.ChangeTracker.Clear(); return null; }
        if (publish is not null) await publish(review, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return review;
    });
    public Task<Review?> FindAsync(int id, CancellationToken ct = default) => OpinionStorage.Run(() => context.Value.Reviews.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct));
    public Task<IReadOnlyList<Review>> QueryAsync(ReviewQuery query, CancellationToken ct = default) => OpinionStorage.Run<IReadOnlyList<Review>>(async () =>
    {
        var rows = context.Value.Reviews.AsNoTracking();
        if (query.GameId is int gameId) rows = rows.Where(item => item.GameId == gameId);
        if (query.UserId is int userId) rows = rows.Where(item => item.UserId == userId);
        if (query.Authors is not null) rows = rows.Where(item => query.Authors.Contains(item.UserId));
        if (query.Ids is not null) rows = rows.Where(item => query.Ids.Contains(item.Id));
        return await rows.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).ToArrayAsync(ct);
    });
    public Task<Review?> EditAsync(int id, int actorId, string? text, DateTimeOffset now, CancellationToken ct = default) => OpinionStorage.Run<Review?>(async () =>
    {
        var db = context.Value;
        var original = await db.Reviews.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (original is null) return null;
        var edited = original.Edit(actorId, text, now);
        var changed = await db.Reviews.Where(item => item.Id == id && item.UserId == actorId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Text, edited.Text).SetProperty(item => item.UpdatedAt, now), ct);
        return changed == 0 ? null : await db.Reviews.AsNoTracking().SingleAsync(item => item.Id == id, ct);
    });
}
public sealed class PostgresRatings(Lazy<TccDbContext> context) : IRatings
{
    public Task<Rating?> FindAsync(int userId, int gameId, CancellationToken ct = default) => OpinionStorage.Run(() => context.Value.Ratings.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.GameId == gameId, ct));
    public Task<Rating> SetAsync(int userId, int gameId, RatingValue value, DateTimeOffset now, CancellationToken ct = default) => OpinionStorage.Run(async () =>
    {
        var db = context.Value;
        var timestamp = now.ToUniversalTime();
        var rows = await db.Ratings.FromSqlInterpolated($"""
            INSERT INTO avaliacoes (usuario_id,jogo_id,nota,criado_em,atualizado_em)
            VALUES ({userId},{gameId},{value.Value},{timestamp},{timestamp})
            ON CONFLICT (usuario_id,jogo_id) DO UPDATE SET nota=EXCLUDED.nota,atualizado_em=GREATEST(avaliacoes.atualizado_em,EXCLUDED.atualizado_em)
            RETURNING *
            """).AsNoTracking().ToArrayAsync(ct);
        return rows.Single();
    });
    public Task<IReadOnlyDictionary<int, double?>> AveragesAsync(IReadOnlyList<int> gameIds, CancellationToken ct = default)
    {
        if (gameIds.Count == 0) return Task.FromResult<IReadOnlyDictionary<int, double?>>(new Dictionary<int, double?>());
        return OpinionStorage.Run<IReadOnlyDictionary<int, double?>>(async () =>
        {
            var ids = gameIds.Distinct().ToArray();
            var values = await context.Value.Database.SqlQuery<SqlRatingAverage>($"""
                SELECT jogo_id AS "GameId", AVG(nota) AS "Average" FROM avaliacoes WHERE jogo_id = ANY({ids}) GROUP BY jogo_id
                """).ToArrayAsync(ct);
            return values.ToDictionary(item => item.GameId, item => (double?)item.Average);
        });
    }
}
public sealed record SqlRatingAverage(int GameId, double Average);
