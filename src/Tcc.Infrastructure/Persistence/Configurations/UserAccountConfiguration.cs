using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Persistence.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> user)
    {
        user.ToTable("usuarios", table =>
        {
            table.HasCheckConstraint("ck_usuarios_id_positivo", "id > 0");
            table.HasCheckConstraint("ck_usuarios_email_normalizado", "email = lower(btrim(email))");
        });
        user.HasKey(account => account.Id);
        user.Property(account => account.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        user.Property(account => account.Username).HasColumnName("username").HasMaxLength(100)
            .HasComputedColumnSql("'user-' || id::text", stored: true);
        user.Property(account => account.Email).HasColumnName("email").HasMaxLength(254)
            .HasConversion(email => email.Value, value => Email.Create(value)).IsRequired();
        user.Property(account => account.PasswordHash).HasColumnName("senha_hash").IsRequired();
        user.Property(account => account.CreatedAt).HasColumnName("criado_em");
        user.Property(account => account.UpdatedAt).HasColumnName("atualizado_em");
        user.HasIndex(account => account.Username).IsUnique();
        user.HasIndex(account => account.Email).IsUnique();
        user.OwnsOne(account => account.Profile, profile =>
        {
            profile.ToTable("perfis_usuarios", table => {
                table.HasCheckConstraint("ck_perfis_banner_position_x", "banner_position_x BETWEEN 0 AND 100");
                table.HasCheckConstraint("ck_perfis_banner_position_y", "banner_position_y BETWEEN 0 AND 100");
            });
            profile.WithOwner().HasForeignKey("UserId").Metadata.DeleteBehavior = DeleteBehavior.Restrict;
            profile.Property<int>("UserId").HasColumnName("usuario_id").ValueGeneratedNever();
            profile.HasKey("UserId");
            profile.Property(item => item.DisplayName).HasColumnName("nome_exibicao").HasMaxLength(100).IsRequired();
            profile.Property(item => item.Bio).HasColumnName("biografia").HasMaxLength(2000).IsRequired();
            profile.Property(item => item.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(2048);
            profile.Property(item => item.BannerUrl).HasColumnName("banner_url").HasMaxLength(2048);
            profile.Property(item => item.BannerPositionX).HasColumnName("banner_position_x").HasDefaultValue(50).HasSentinel(-1);
            profile.Property(item => item.BannerPositionY).HasColumnName("banner_position_y").HasDefaultValue(50).HasSentinel(-1);
            profile.Property(item => item.Country).HasColumnName("pais").HasMaxLength(100);
            profile.Property(item => item.State).HasColumnName("estado").HasMaxLength(100);
            profile.Property(item => item.UpdatedAt).HasColumnName("atualizado_em");
        });
        user.Navigation(account => account.Profile).HasField("profile").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();
    }
}
