using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tcc.Infrastructure.Persistence;

public sealed class TccDbContextFactory : IDesignTimeDbContextFactory<TccDbContext>
{
    public TccDbContext CreateDbContext(string[] args)
    {
        var settings = DatabaseSettings.Read(name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process));
        var options = new DbContextOptionsBuilder<TccDbContext>().UseNpgsql(settings.ConnectionString)
            .UseLoggerFactory(NullLoggerFactory.Instance).EnableSensitiveDataLogging(false).Options;
        return new(options);
    }
}
