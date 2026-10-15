using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.Domain.Identity;
using Tcc.Domain.Catalog;
using Tcc.Domain.Opinions;
using Tcc.Domain.Social;

namespace Tcc.Infrastructure.Persistence.Configurations;

public sealed class GameFavoriteConfiguration : IEntityTypeConfiguration<GameFavorite>
{
    public void Configure(EntityTypeBuilder<GameFavorite> entity)
    {
        entity.ToTable("jogos_favoritos");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.UserId).HasColumnName("usuario_id");
        entity.Property(item => item.GameId).HasColumnName("jogo_id");
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Game>().WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.UserId, item.GameId }).IsUnique();
        entity.HasIndex(item => item.GameId);
    }
}
public sealed class ReviewFavoriteConfiguration : IEntityTypeConfiguration<ReviewFavorite>
{
    public void Configure(EntityTypeBuilder<ReviewFavorite> entity)
    {
        entity.ToTable("reviews_favoritas");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.UserId).HasColumnName("usuario_id");
        entity.Property(item => item.ReviewId).HasColumnName("review_id");
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Review>().WithMany().HasForeignKey(item => item.ReviewId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.UserId, item.ReviewId }).IsUnique();
        entity.HasIndex(item => item.ReviewId);
    }
}
public sealed class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> entity)
    {
        entity.ToTable("seguimentos_usuarios", table => table.HasCheckConstraint("ck_seguimentos_sem_autoseguimento", "seguidor_id <> seguido_id"));
        entity.HasKey(item => new { item.FollowerId, item.FollowedId });
        entity.Property(item => item.FollowerId).HasColumnName("seguidor_id");
        entity.Property(item => item.FollowedId).HasColumnName("seguido_id");
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.FollowerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.FollowedId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.FollowedId, item.FollowerId });
    }
}
