using Microsoft.EntityFrameworkCore;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Persistence;

public sealed class TccDbContext(DbContextOptions<TccDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<Tcc.Domain.Catalog.Game> Games => Set<Tcc.Domain.Catalog.Game>();
    public DbSet<Tcc.Domain.Catalog.Genre> Genres => Set<Tcc.Domain.Catalog.Genre>();
    public DbSet<Tcc.Domain.Catalog.Platform> Platforms => Set<Tcc.Domain.Catalog.Platform>();
    public DbSet<Tcc.Domain.Catalog.GameGenre> GameGenres => Set<Tcc.Domain.Catalog.GameGenre>();
    public DbSet<Tcc.Domain.Catalog.GamePlatform> GamePlatforms => Set<Tcc.Domain.Catalog.GamePlatform>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(TccDbContext).Assembly);
}
