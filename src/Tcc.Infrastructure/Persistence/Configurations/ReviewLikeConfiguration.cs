using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.Domain.Identity;
using Tcc.Domain.Opinions;

namespace Tcc.Infrastructure.Persistence.Configurations;

public sealed class ReviewLikeConfiguration : IEntityTypeConfiguration<ReviewLike>
{
    public void Configure(EntityTypeBuilder<ReviewLike> entity)
    {
        entity.ToTable("curtidas_reviews");
        entity.HasKey(item => new { item.UserId, item.ReviewId });
        entity.Property(item => item.UserId).HasColumnName("usuario_id");
        entity.Property(item => item.ReviewId).HasColumnName("review_id");
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Review>().WithMany().HasForeignKey(item => item.ReviewId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => item.ReviewId);
    }
}
