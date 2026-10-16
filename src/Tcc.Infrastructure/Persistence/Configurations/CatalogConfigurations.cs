using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tcc.Domain.Catalog;

namespace Tcc.Infrastructure.Persistence.Configurations;

public sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> entity)
    {
        entity.ToTable("jogos", table => table.HasCheckConstraint("ck_jogos_igdb_positivo", "igdb_id > 0"));
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.IgdbId).HasColumnName("igdb_id");
        entity.HasIndex(item => item.IgdbId).IsUnique();
        entity.Property(item => item.Title).HasColumnName("nome").IsRequired();
        entity.Property(item => item.Summary).HasColumnName("descricao").IsRequired();
        entity.Property(item => item.CoverUrl).HasColumnName("capa_url");
        entity.Property(item => item.ReleaseDate).HasColumnName("primeira_data_lancamento");
        entity.Property(item => item.FetchedAt).HasColumnName("dados_obtidos_em");
        entity.Property(item => item.CreatedAt).HasColumnName("criado_em");
        entity.Property(item => item.UpdatedAt).HasColumnName("atualizado_em");
        entity.Ignore(item => item.Genres); entity.Ignore(item => item.Platforms);
        entity.Ignore(item => item.GenreEntries); entity.Ignore(item => item.PlatformEntries);
    }
}
public sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> entity)
    {
        entity.ToTable("generos", table => table.HasCheckConstraint("ck_generos_igdb_positivo", "igdb_id > 0"));
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.IgdbId).HasColumnName("igdb_id");
        entity.Property(item => item.Name).HasColumnName("nome").IsRequired();
        entity.HasIndex(item => item.IgdbId).IsUnique();
    }
}
public sealed class PlatformConfiguration : IEntityTypeConfiguration<Platform>
{
    public void Configure(EntityTypeBuilder<Platform> entity)
    {
        entity.ToTable("plataformas", table => table.HasCheckConstraint("ck_plataformas_igdb_positivo", "igdb_id > 0"));
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(item => item.IgdbId).HasColumnName("igdb_id");
        entity.Property(item => item.Name).HasColumnName("nome").IsRequired();
        entity.HasIndex(item => item.IgdbId).IsUnique();
    }
}
public sealed class GameGenreConfiguration : IEntityTypeConfiguration<GameGenre>
{
    public void Configure(EntityTypeBuilder<GameGenre> entity)
    {
        entity.ToTable("jogos_generos"); entity.HasKey(item => new { item.GameId, item.GenreId });
        entity.Property(item => item.GameId).HasColumnName("jogo_id").ValueGeneratedNever();
        entity.Property(item => item.GenreId).HasColumnName("genero_id").ValueGeneratedNever();
        entity.HasOne<Game>().WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Genre>().WithMany().HasForeignKey(item => item.GenreId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class GamePlatformConfiguration : IEntityTypeConfiguration<GamePlatform>
{
    public void Configure(EntityTypeBuilder<GamePlatform> entity)
    {
        entity.ToTable("jogos_plataformas"); entity.HasKey(item => new { item.GameId, item.PlatformId });
        entity.Property(item => item.GameId).HasColumnName("jogo_id").ValueGeneratedNever();
        entity.Property(item => item.PlatformId).HasColumnName("plataforma_id").ValueGeneratedNever();
        entity.HasOne<Game>().WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Platform>().WithMany().HasForeignKey(item => item.PlatformId).OnDelete(DeleteBehavior.Restrict);
    }
}
