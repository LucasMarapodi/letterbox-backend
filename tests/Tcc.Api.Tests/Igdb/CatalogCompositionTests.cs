using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class CatalogCompositionTests
{
    [Fact]
    public async Task Host_composes_shared_catalog_stores_without_reading_credentials_for_known_local_game()
    {
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(testCatalogBuilder => testCatalogBuilder.ConfigureServices(testCatalogServices => Tcc.Api.Tests.Fixtures.TestCatalogRegistration.AddTestCatalog(testCatalogServices))).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IgdbCredentialOptions>(_ =>
                throw new InvalidOperationException("Known local data must not depend on credentials"))));
        using var a = host.Services.CreateScope();
        using var b = host.Services.CreateScope();
        var local = a.ServiceProvider.GetRequiredService<ILocalGameCatalog>();
        await local.SaveGamesAsync([CatalogFlow.Game(1)], default);
        var first = a.ServiceProvider.GetRequiredService<IAsyncGameCatalog>();
        var second = b.ServiceProvider.GetRequiredService<IAsyncGameCatalog>();
        Assert.Same(first, second);
        Assert.NotNull(await first.GetAsync(1));
        Assert.NotNull(await second.GetAsync(1));
        await using var other = new WebApplicationFactory<Program>().WithWebHostBuilder(testCatalogBuilder => testCatalogBuilder.ConfigureServices(testCatalogServices => Tcc.Api.Tests.Fixtures.TestCatalogRegistration.AddTestCatalog(testCatalogServices)));
        Assert.Empty(await other.Services.GetRequiredService<ILocalGameCatalog>().FindGamesAsync([1], default));
    }
}
