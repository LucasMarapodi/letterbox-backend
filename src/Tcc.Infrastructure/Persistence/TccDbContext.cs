using Microsoft.EntityFrameworkCore;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Persistence;

public sealed class TccDbContext(DbContextOptions<TccDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<UserSession> Sessions => Set<UserSession>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(TccDbContext).Assembly);
}
