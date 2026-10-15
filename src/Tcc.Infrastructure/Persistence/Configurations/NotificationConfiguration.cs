using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.Domain.Identity;
using Tcc.Domain.Opinions;
using Tcc.Domain.Social;

namespace Tcc.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> entity)
    {
        entity.ToTable("notificacoes", table =>
        {
            table.HasCheckConstraint("ck_notificacoes_tipo_comentario", "(tipo = 'new-review' AND comentario_id IS NULL) OR (tipo = 'review-comment' AND comentario_id IS NOT NULL)");
            table.HasCheckConstraint("ck_notificacoes_sem_auto", "destinatario_id <> ator_id");
        });
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.UserId).HasColumnName("destinatario_id");
        entity.Property(item => item.ActorId).HasColumnName("ator_id");
        entity.Property(item => item.ReviewId).HasColumnName("review_id");
        entity.Property(item => item.CommentId).HasColumnName("comentario_id");
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.Property(item => item.Type).HasColumnName("tipo").HasMaxLength(20).HasConversion(
            type => type == NotificationType.NewReview ? "new-review" : "review-comment",
            type => type == "new-review" ? NotificationType.NewReview : NotificationType.ReviewComment);
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Review>().WithMany().HasForeignKey(item => item.ReviewId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Comment>().WithMany().HasForeignKey(item => new { item.ReviewId, item.CommentId })
            .HasPrincipalKey(item => new { item.ReviewId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.UserId, item.CreatedAt, item.Id });
        entity.HasIndex(item => new { item.UserId, item.ReviewId }).IsUnique().HasFilter("tipo = 'new-review'");
        entity.HasIndex(item => new { item.UserId, item.CommentId }).IsUnique().HasFilter("tipo = 'review-comment'");
    }
}
