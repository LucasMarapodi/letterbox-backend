using Microsoft.EntityFrameworkCore;
using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresComments(Lazy<TccDbContext> context) : IComments
{
    public Task<Comment> AddAsync(Comment comment, CancellationToken ct = default, Func<Comment, CancellationToken, Task>? publish = null) => OpinionStorage.Run(async () =>
    {
        var db = context.Value;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        if (publish is not null) await publish(comment, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return comment;
    });
    public Task<Comment?> FindAsync(int id, CancellationToken ct = default) => OpinionStorage.Run(() => context.Value.Comments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct));
    public Task<IReadOnlyList<CommentWithLikes>> ForReviewAsync(int reviewId, CancellationToken ct = default) => OpinionStorage.Run<IReadOnlyList<CommentWithLikes>>(async () =>
    {
        var db = context.Value;
        return await db.Comments.AsNoTracking().Where(item => item.ReviewId == reviewId).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new CommentWithLikes(item, db.CommentLikes.Count(like => like.CommentId == item.Id))).ToArrayAsync(ct);
    });
    public Task<IReadOnlyList<CommentActivitySummary>> ActivityAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) => OpinionStorage.Run<IReadOnlyList<CommentActivitySummary>>(async () =>
        await context.Value.Comments.AsNoTracking().Where(item => item.CreatedAt >= from && item.CreatedAt <= to).GroupBy(item => item.ReviewId)
            .Select(group => new CommentActivitySummary(group.Key, group.Count(), group.Max(item => item.CreatedAt))).ToArrayAsync(ct));
}
public sealed class PostgresCommentLikes(Lazy<TccDbContext> context) : ICommentLikes
{
    public Task SetAsync(int userId, int commentId, DateTimeOffset now, CancellationToken ct = default) => OpinionStorage.Run(async () =>
        await context.Value.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO curtidas_comentarios(usuario_id,comentario_id,criado_em) VALUES({userId},{commentId},{now.ToUniversalTime()}) ON CONFLICT (usuario_id,comentario_id) DO NOTHING", ct));
    public Task RemoveAsync(int userId, int commentId, CancellationToken ct = default) => OpinionStorage.Run(async () =>
        await context.Value.CommentLikes.Where(item => item.UserId == userId && item.CommentId == commentId).ExecuteDeleteAsync(ct));
    public Task<bool> HasAsync(int userId, int commentId, CancellationToken ct = default) => OpinionStorage.Run(() =>
        context.Value.CommentLikes.AsNoTracking().AnyAsync(item => item.UserId == userId && item.CommentId == commentId, ct));
}
