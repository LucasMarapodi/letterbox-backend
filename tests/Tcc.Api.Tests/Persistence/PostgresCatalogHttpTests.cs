using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tcc.Api.Configuration;
using Tcc.Api.Tests.Identity;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class PostgresCatalogHttpTests
{
    [PostgresFact]
    public async Task Search_persists_before_cache_and_both_local_and_explicit_external_lookup_survive_restart_without_supplier()
    {
        await PostgresCatalogTests.Reset();
        var supplier = new Supplier();
        await using var first = new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IExternalGameCatalog>(supplier)));
        using var client = first.CreateClient();
        using var page = await client.GetAsync("/api/v1/games?term=Persistent&pageSize=2");
        Assert.Equal(200, (int)page.StatusCode);
        using var json = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        var game = json.RootElement.GetProperty("items")[0];
        var id = game.GetProperty("id").GetInt32();
        Assert.True(id > 0);
        Assert.Equal(3_000_000_001, game.GetProperty("igdbId").GetInt64());
        Assert.Equal(1, supplier.Calls);
        using var repeated = await client.GetAsync("/api/v1/games?term=Persistent&pageSize=2");
        Assert.Equal(200, (int)repeated.StatusCode);
        Assert.Equal(Assert.Single(page.Headers.GetValues("X-Catalog-Fetched-At")), Assert.Single(repeated.Headers.GetValues("X-Catalog-Fetched-At")));
        var cache = Assert.IsType<InMemoryCatalogCache>(first.Services.GetRequiredService<ICatalogCache>());
        Assert.True(cache.EvictGame(id));
        var original = await client.GetStringAsync("/api/v1/games/" + id);
        Assert.Equal(1, supplier.Calls);
        var offline = new PostgresCatalogTests.NoSupplier();
        await using var restarted = new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IExternalGameCatalog>(offline)));
        using var other = restarted.CreateClient();
        Assert.Equal(original, await other.GetStringAsync("/api/v1/games/" + id));
        using var external = await other.GetAsync("/api/v1/games/igdb/3000000001");
        Assert.Equal(200, (int)external.StatusCode);
        using var missing = await other.GetAsync("/api/v1/games/999999");
        Assert.Equal(404, (int)missing.StatusCode);
        Assert.Equal(0, offline.Calls);
        using var imported = await client.GetAsync("/api/v1/games/igdb/3000000010");
        Assert.Equal(200, (int)imported.StatusCode);
        Assert.Equal(2, supplier.Calls);
        using var importedAgain = await client.GetAsync("/api/v1/games/igdb/3000000010");
        Assert.Equal(200, (int)importedAgain.StatusCode);
        Assert.Equal(2, supplier.Calls);
    }

    [Fact]
    public async Task Cold_SQL_failure_returns_storage_503_without_calling_IGDB_or_populating_cache()
    {
        Assert.DoesNotContain(typeof(IgdbGameCatalog).Assembly.GetTypes(), type => type.Name == "InMemoryLocalGameCatalog");
        var supplier = new Supplier();
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddPostgresPersistence(_ => "Host=127.0.0.1;Port=1;Database=unavailable;Username=synthetic;Password=synthetic;Timeout=1");
                services.AddSingleton<IExternalGameCatalog>(supplier);
            });
        });
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/games?term=Persistent");
        Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal("storage_unavailable", (await response.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        Assert.Equal(0, supplier.Calls);
        Assert.Null(await host.Services.GetRequiredService<ICatalogCache>().FindPageAsync(CatalogSearch.Create("Persistent"), default));
    }

    private sealed class Supplier : IExternalGameCatalog
    {
        public int Calls { get; private set; }
        public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken ct)
        {
            Calls++;
            var now = DateTimeOffset.UtcNow;
            var game = PostgresCatalogTests.Payload(fetched: now.AddTicks(7 - now.Ticks % 10));
            return Task.FromResult(new CatalogPage(query, [game], false, game.Metadata));
        }
        public Task<IReadOnlyDictionary<long, CatalogGame>> ResolveAsync(IReadOnlyList<long> ids, CancellationToken ct)
        { Calls++; return Task.FromResult<IReadOnlyDictionary<long, CatalogGame>>(ids.ToDictionary(id => id, id => PostgresCatalogTests.Payload(id))); }
    }
}
