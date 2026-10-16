using Microsoft.EntityFrameworkCore;
using Tcc.Application.Social;
using Tcc.Domain.Social;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresNotifications(Lazy<TccDbContext> context) : INotifications
{
    public Task AddRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken ct = default) => OpinionStorage.Run(async () =>
    {
        var db = context.Value;
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Eventos devem participar da transação de publicação.");
        foreach (var chunk in notifications.Chunk(500))
        {
            var recipients = chunk.Select(item => item.UserId).ToArray();
            var actors = chunk.Select(item => item.ActorId).ToArray();
            var reviews = chunk.Select(item => item.ReviewId).ToArray();
            var comments = chunk.Select(item => item.CommentId).ToArray();
            var types = chunk.Select(item => item.Type == NotificationType.NewReview ? "new-review" : "review-comment").ToArray();
            var dates = chunk.Select(item => item.CreatedAt).ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notificacoes(destinatario_id,ator_id,review_id,comentario_id,tipo,criado_em)
                SELECT * FROM unnest({recipients}::integer[],{actors}::integer[],{reviews}::integer[],
                    {comments}::integer[],{types}::text[],{dates}::timestamptz[])
                ON CONFLICT DO NOTHING
                """, ct);
        }
        return true;
    });
    public Task<IReadOnlyList<Notification>> ForUserAsync(int userId, CancellationToken ct = default) => OpinionStorage.Run<IReadOnlyList<Notification>>(async () =>
        await context.Value.Notifications.AsNoTracking().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).ToArrayAsync(ct));
}
