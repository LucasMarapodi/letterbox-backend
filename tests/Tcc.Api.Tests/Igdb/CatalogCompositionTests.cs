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
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IgdbCredentialOptions>(_ =>
                throw new InvalidOperationException("Known local data must not depend on credentials"))));
        using var a = host.Services.CreateScope();
        using var b = host.Services.CreateScope();
        var local = a.ServiceProvider.GetRequiredService<ILocalGameCatalog>();
        await local.SaveGamesAsync([CatalogFlow.Game("igdb-1")], default);
        var first = a.ServiceProvider.GetRequiredService<IAsyncGameCatalog>();
        var second = b.ServiceProvider.GetRequiredService<IAsyncGameCatalog>();
        Assert.Same(first, second);
        Assert.NotNull(await first.GetAsync("igdb-1"));
        Assert.NotNull(await second.GetAsync("igdb-1"));
        await using var other = new WebApplicationFactory<Program>();
        Assert.Empty(await other.Services.GetRequiredService<ILocalGameCatalog>().FindGamesAsync(["igdb-1"], default));
    }
}
