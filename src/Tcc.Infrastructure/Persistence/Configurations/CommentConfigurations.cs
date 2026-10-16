using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.Domain.Opinions;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Persistence.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> entity)
    {
        entity.ToTable("comentarios", table =>
        {
            table.HasCheckConstraint("ck_comentarios_texto", "char_length(texto) BETWEEN 1 AND 2000 AND texto ~ '[^[:space:]]'");
            table.HasCheckConstraint("ck_comentarios_sem_autopai", "comentario_pai_id IS NULL OR comentario_pai_id <> id");
        });
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.UserId).HasColumnName("usuario_id");
        entity.Property(item => item.ReviewId).HasColumnName("review_id");
        entity.Property(item => item.ParentCommentId).HasColumnName("comentario_pai_id");
        entity.Property(item => item.Text).HasColumnName("texto").HasMaxLength(2000).IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.HasAlternateKey(item => new { item.ReviewId, item.Id });
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Review>().WithMany().HasForeignKey(item => item.ReviewId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Comment>().WithMany().HasForeignKey(item => new { item.ReviewId, item.ParentCommentId })
            .HasPrincipalKey(item => new { item.ReviewId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.ReviewId, item.CreatedAt, item.Id });
        entity.HasIndex(item => new { item.CreatedAt, item.ReviewId });
    }
}
public sealed class CommentLikeConfiguration : IEntityTypeConfiguration<CommentLike>
{
    public void Configure(EntityTypeBuilder<CommentLike> entity)
    {
        entity.ToTable("curtidas_comentarios");
        entity.HasKey(item => new { item.UserId, item.CommentId });
        entity.Property(item => item.UserId).HasColumnName("usuario_id");
        entity.Property(item => item.CommentId).HasColumnName("comentario_id");
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Comment>().WithMany().HasForeignKey(item => item.CommentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => item.CommentId);
    }
}
