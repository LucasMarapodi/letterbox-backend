using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.Domain.Catalog;
using Tcc.Domain.Forum;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Persistence.Configurations;

public sealed class ForumTopicConfiguration : IEntityTypeConfiguration<ForumTopic>
{
    public void Configure(EntityTypeBuilder<ForumTopic> entity)
    {
        entity.ToTable("topicos_forum", table =>
        {
            table.HasCheckConstraint("ck_topicos_forum_titulo", "char_length(titulo) BETWEEN 1 AND 150 AND titulo ~ '[^[:space:]]'");
            table.HasCheckConstraint("ck_topicos_forum_texto", "char_length(texto) BETWEEN 1 AND 10000 AND texto ~ '[^[:space:]]'");
        });
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.GameId).HasColumnName("jogo_id");
        entity.Property(item => item.UserId).HasColumnName("usuario_id");
        entity.Property(item => item.Title).HasColumnName("titulo").HasMaxLength(150).IsRequired();
        entity.Property(item => item.Text).HasColumnName("texto").HasMaxLength(10000).IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.HasOne<Game>().WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.GameId, item.CreatedAt, item.Id });
    }
}

public sealed class ForumReplyConfiguration : IEntityTypeConfiguration<ForumReply>
{
    public void Configure(EntityTypeBuilder<ForumReply> entity)
    {
        entity.ToTable("respostas_forum", table => table.HasCheckConstraint("ck_respostas_forum_texto",
            "char_length(texto) BETWEEN 1 AND 10000 AND texto ~ '[^[:space:]]'"));
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.TopicId).HasColumnName("topico_id");
        entity.Property(item => item.UserId).HasColumnName("usuario_id");
        entity.Property(item => item.Text).HasColumnName("texto").HasMaxLength(10000).IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.HasOne<ForumTopic>().WithMany().HasForeignKey(item => item.TopicId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.TopicId, item.CreatedAt, item.Id });
    }
}
