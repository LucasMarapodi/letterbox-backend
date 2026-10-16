using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Persistence.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> session)
    {
        session.ToTable("sessoes_usuarios", table =>
        {
            table.HasCheckConstraint("ck_sessoes_prazo", "expira_em > criado_em");
            table.HasCheckConstraint("ck_sessoes_hash", "token_hash ~ '^[0-9a-f]{64}$'");
        });
        session.HasKey(item => item.Id);
        session.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        session.Property(item => item.UserId).HasColumnName("usuario_id");
        session.Property(item => item.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        session.Property(item => item.CreatedAt).HasColumnName("criado_em");
        session.Property(item => item.ExpiresAt).HasColumnName("expira_em");
        session.Property(item => item.RevokedAt).HasColumnName("revogado_em");
        session.HasIndex(item => item.TokenHash).IsUnique();
        session.HasIndex(item => item.UserId);
        session.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
