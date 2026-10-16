using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.Domain.Opinions;
using Tcc.Domain.Identity;
using Tcc.Domain.Catalog;

namespace Tcc.Infrastructure.Persistence.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> entity)
    {
        entity.ToTable("reviews", table => table.HasCheckConstraint("ck_reviews_texto", "char_length(texto) <= 10000"));
        entity.HasAlternateKey(item => new { item.UserId, item.GameId });
        entity.HasOne(item => item.Evaluation).WithOne().HasForeignKey<Rating>(item => new { item.UserId, item.GameId })
            .HasPrincipalKey<Review>(item => new { item.UserId, item.GameId }).OnDelete(DeleteBehavior.Restrict);
        entity.Navigation(item => item.Evaluation).AutoInclude();
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.UserId).HasColumnName("usuario_id");
        entity.Property(item => item.GameId).HasColumnName("jogo_id");
        entity.Property(item => item.Text).HasColumnName("texto").HasMaxLength(10000).IsRequired();
        entity.Property(item => item.Title).HasColumnName("titulo").HasMaxLength(150);
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.Property(item => item.UpdatedAt).HasColumnName("atualizado_em");
        entity.HasIndex(item => new { item.UserId, item.GameId }).IsUnique();
        entity.HasIndex(item => new { item.GameId, item.CreatedAt, item.Id });
        entity.HasIndex(item => new { item.UserId, item.CreatedAt, item.Id });
        entity.HasIndex(item => new { item.CreatedAt, item.Id });
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Game>().WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> entity)
    {
        entity.ToTable("avaliacoes", table => table.HasCheckConstraint("ck_avaliacoes_nota", "nota >= 0 AND nota <= 5 AND nota <> 'NaN'::double precision"));
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.UserId).HasColumnName("usuario_id");
        entity.Property(item => item.GameId).HasColumnName("jogo_id");
        entity.Property(item => item.Value).HasColumnName("nota").HasColumnType("double precision").HasConversion(value => value.Value, value => RatingValue.Create(value));
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.Property(item => item.UpdatedAt).HasColumnName("atualizado_em");
        entity.HasIndex(item => new { item.UserId, item.GameId }).IsUnique();
        entity.HasIndex(item => item.GameId);
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Game>().WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Restrict);
    }
}
