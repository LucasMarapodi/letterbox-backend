using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.DependencyInjection;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

public sealed class BootstrapTests
{
    internal const string SyntheticConnection = "Host=localhost;Database=tcc_model_only;Username=synthetic;Password=synthetic-not-real;Timeout=2";

    [Fact]
    public void DbContext_is_scoped_uses_Npgsql_without_connecting_and_does_not_disclose_connection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPostgresPersistence(_ => SyntheticConnection);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var a = provider.CreateScope();
        using var b = provider.CreateScope();
        var first = a.ServiceProvider.GetRequiredService<TccDbContext>();
        Assert.Same(first, a.ServiceProvider.GetRequiredService<TccDbContext>());
        Assert.NotSame(first, b.ServiceProvider.GetRequiredService<TccDbContext>());
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", first.Database.ProviderName);
        Assert.False(first.GetService<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptions>().Extensions
            .OfType<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>().Single().IsSensitiveDataLoggingEnabled);
    }

    [Fact]
    public void Missing_connection_fails_safely_when_context_is_requested()
    {
        var services = new ServiceCollection();
        services.AddPostgresPersistence(_ => null);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var failure = Assert.ThrowsAny<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<TccDbContext>());
        Assert.Contains("ConnectionStrings__TccDatabase", failure.Message);
    }
}
