using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Application.Catalog;
using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class CacheEvictionRegressionTests
{
    [Fact]
    public async Task Targeted_eviction_preserves_other_entries_local_catalog_and_offline_projection()
    {
        var supplier = new OfflineCatalog();
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(testCatalogBuilder => testCatalogBuilder.ConfigureServices(testCatalogServices => Tcc.Api.Tests.Fixtures.TestCatalogRegistration.AddTestCatalog(testCatalogServices))).WithWebHostBuilder(identityBuilder => identityBuilder.ConfigureServices(identityServices => Tcc.Api.Tests.Fixtures.TestIdentityRegistration.AddTestIdentity(identityServices))).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IExternalGameCatalog>(supplier)));
        var cache = Assert.IsType<InMemoryCatalogCache>(host.Services.GetRequiredService<ICatalogCache>());
        var local = host.Services.GetRequiredService<ILocalGameCatalog>();
        var games = new[] { CatalogFlow.Game(10), CatalogFlow.Game(11) };
        await local.SaveGamesAsync(games, default);
        await cache.SaveGamesAsync(games, default);
        host.Services.GetRequiredService<IReviews>().TryAdd(Review.Create(105, 1, 10, "Retained", CatalogFlow.Metadata.FetchedAt));
        Assert.True(cache.EvictGame(10));
        Assert.False(cache.EvictGame(10));
        Assert.Equal(11, Assert.Single(await cache.FindGamesAsync([10, 11], default)).Key);
        Assert.Equal(2, (await local.FindGamesAsync([10, 11], default)).Count);
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/community/reviews/recent");
        response.EnsureSuccessStatusCode();
        Assert.Equal(0, supplier.Calls);
        Assert.Single(await cache.FindGamesAsync([10], default));
    }
}
