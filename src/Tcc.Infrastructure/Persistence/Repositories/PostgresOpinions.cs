using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Application;
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
    public Task<Review> WriteAsync(ReviewWrite command, Func<Review, CancellationToken, Task> publish, CancellationToken ct = default) => OpinionStorage.Run(async () =>
    {
        var db = context.Value;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Shared across POST, PATCH and legacy rating PUT; read after acquiring the lock.
        await ProfileWriteLock.Acquire(db, command.UserId, ct);
        var review = await db.Reviews.AsTracking().SingleOrDefaultAsync(item => item.UserId == command.UserId && item.GameId == command.GameId, ct);
        var creating = review is null;
        if (command.ReviewId is int id && (review is null || review.Id != id)) throw new ResourceFailure(ResourceFailureKind.NotFound);
        if (review is not null && command.CreateOnly) throw new ResourceFailure(ResourceFailureKind.Conflict);
        if (review is null)
        {
            review = Review.CreateRated(command.UserId, command.GameId, command.Text, command.Rating, command.Now, command.Title);
            db.Reviews.Add(review);
        }
        else
        {
            var updated = review.EditRated(command.UserId, command.TextSpecified, command.Text, command.Rating, command.Now, command.TitleSpecified, command.Title);
            db.Entry(review).CurrentValues.SetValues(updated);
            if (review.Evaluation is null) db.Ratings.Add(updated.Evaluation!);
            else db.Entry(review.Evaluation).CurrentValues.SetValues(updated.Evaluation!);
        }
        await db.SaveChangesAsync(ct);
        if (creating) await publish(review, ct);
        await db.SaveChangesAsync(ct);
        var saved = await db.Reviews.AsNoTracking().SingleAsync(item => item.Id == review.Id, ct);
        await tx.CommitAsync(ct);
        return saved;
    });
    public Task<Review?> FindAsync(int id, CancellationToken ct = default) => OpinionStorage.Run(() => context.Value.Reviews.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct));
    public Task<IReadOnlyList<Review>> QueryAsync(ReviewQuery query, CancellationToken ct = default) => OpinionStorage.Run<IReadOnlyList<Review>>(async () =>
    {
        var rows = context.Value.Reviews.AsNoTracking();
        if (query.GameId is int gameId) rows = rows.Where(item => item.GameId == gameId);
        if (query.UserId is int userId) rows = rows.Where(item => item.UserId == userId);
        if (query.Authors is not null) rows = rows.Where(item => query.Authors.Contains(item.UserId));
        if (query.Ids is not null) rows = rows.Where(item => query.Ids.Contains(item.Id));
        return await ReviewOrdering.Common(rows).ToArrayAsync(ct);
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
