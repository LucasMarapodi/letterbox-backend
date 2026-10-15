using Microsoft.EntityFrameworkCore;

namespace Tcc.Infrastructure.Persistence;

public sealed class TccDbContext(DbContextOptions<TccDbContext> options) : DbContext(options);
