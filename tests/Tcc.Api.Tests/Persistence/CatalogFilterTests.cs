using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Configuration;
using Tcc.Api.Tests.Igdb;
using Tcc.Application.Catalog;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class CatalogFilterTests
{
    [Theory]
    [InlineData("genreId=0")]
    [InlineData("genreId=-1")]
    [InlineData("platformId=0")]
    [InlineData("platformId=-2")]
    public async Task Nonpositive_filter_is_rejected_before_supplier(string query)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/games?" + query);
        Assert.Equal(400, (int)response.StatusCode);
    }

    [PostgresFact]
    public async Task Options_sync_all_metadata_to_stable_local_ids_and_do_not_depend_on_displayed_games()
    {
        await PostgresCatalogTests.Reset();
        var supplier = new Transport();
        await using var host = Host(supplier);
        using var client = host.CreateClient();
        var first = await client.GetStringAsync("/api/v1/games/filters");
        using var json = JsonDocument.Parse(first);
        Assert.Equal(2, json.RootElement.GetProperty("genres").GetArrayLength());
        Assert.Equal(2, json.RootElement.GetProperty("platforms").GetArrayLength());
        foreach (var group in new[] { "genres", "platforms" })
            foreach (var option in json.RootElement.GetProperty(group).EnumerateArray())
            {
                Assert.InRange(option.GetProperty("id").GetInt32(), 1, int.MaxValue);
                Assert.Equal(new[] { "id", "name" }, option.EnumerateObject().Select(property => property.Name).Order());
            }
        Assert.Equal(first, await client.GetStringAsync("/api/v1/games/filters"));
        Assert.Equal(2, supplier.MetadataCalls);
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(0, await db.Games.CountAsync());
        await using var restarted = Host(new Transport());
        using var other = restarted.CreateClient();
        Assert.Equal(first, await other.GetStringAsync("/api/v1/games/filters"));
    }

    [PostgresFact]
    public async Task Combined_filters_reach_IGDB_as_external_ids_and_separate_cache_and_SQL_snapshots()
    {
        await PostgresCatalogTests.Reset();
        var supplier = new Transport();
        await using var host = Host(supplier);
        using var client = host.CreateClient();
        using var options = JsonDocument.Parse(await client.GetStringAsync("/api/v1/games/filters"));
        var genre = options.RootElement.GetProperty("genres").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "RPG").GetProperty("id").GetInt32();
        var platform = options.RootElement.GetProperty("platforms").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "PC").GetProperty("id").GetInt32();
        var query = $"/api/v1/games?term=Game&genreId={genre}&platformId={platform}&pageSize=1";
        var first = await client.GetStringAsync(query);
        Assert.Contains("genres = (3000000012)", supplier.Bodies.Last());
        Assert.Contains("platforms = (3000000006)", supplier.Bodies.Last());
        Assert.Equal(first, await client.GetStringAsync(query));
        Assert.Equal(1, supplier.GameCalls);
        await client.GetStringAsync($"/api/v1/games?term=Game&genreId={genre}&pageSize=1");
        Assert.Equal(2, supplier.GameCalls);
        using var invalid = await client.GetAsync("/api/v1/games?genreId=2147483647");
        Assert.Equal(400, (int)invalid.StatusCode);
        using var error = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.Equal("invalid_filter", error.RootElement.GetProperty("code").GetString());
        Assert.Equal(2, supplier.GameCalls);
    }

    [PostgresFact]
    public async Task Releases_sort_complete_ties_by_local_id_before_pagination_and_exclude_future_and_missing_dates()
    {
        await PostgresCatalogTests.Reset();
        await using var db = PostgresTestDatabase.Context();
        // Existing identity reverses external-ID order: 99 receives the smallest local ID.
        var original = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload(99)], default)).Game;
        var supplier = new Transport { Releases = true };
        await using var host = Host(supplier);
        using var client = host.CreateClient();
        using var first = JsonDocument.Parse(await client.GetStringAsync("/api/v1/games?pageSize=1"));
        using var second = JsonDocument.Parse(await client.GetStringAsync("/api/v1/games?pageSize=1&page=2"));
        var one = Assert.Single(first.RootElement.GetProperty("items").EnumerateArray());
        var two = Assert.Single(second.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(original.Id, one.GetProperty("id").GetInt32());
        Assert.NotEqual(one.GetProperty("id").GetInt32(), two.GetProperty("id").GetInt32());
        Assert.Equal("2024-01-01", one.GetProperty("releaseDate").GetString());
        Assert.Equal("2024-01-01", two.GetProperty("releaseDate").GetString());
    }

    internal static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host(Transport supplier) =>
        new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton(IgdbTest.Options());
            services.AddHttpClient(IgdbAuthenticationRegistration.OAuthClientName).ConfigurePrimaryHttpMessageHandler(() =>
                new TestHandler((_, _) => Task.FromResult(IgdbTest.TokenResponse())));
            services.AddHttpClient(IgdbAuthenticationRegistration.IgdbClientName).ConfigurePrimaryHttpMessageHandler(() => new TestHandler(supplier.Send));
        }));

    internal sealed class Transport
    {
        public int MetadataCalls { get; private set; }
        public int GameCalls { get; private set; }
        public bool Releases { get; init; }
        public List<string> Bodies { get; } = [];
        public async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            Bodies.Add(body);
            if (request.RequestUri!.AbsolutePath.EndsWith("/genres", StringComparison.Ordinal))
            { MetadataCalls++; return IgdbTest.Json("""[{"id":3000000012,"name":"RPG"},{"id":3000000031,"name":"Adventure"}]"""); }
            if (request.RequestUri.AbsolutePath.EndsWith("/platforms", StringComparison.Ordinal))
            { MetadataCalls++; return IgdbTest.Json("""[{"id":3000000006,"name":"PC"},{"id":3000000048,"name":"Console"}]"""); }
            GameCalls++;
            var rows = new[]
            {
                new { id = 10L, name = "Game A", first_release_date = 1704067200L },
                new { id = 99L, name = "Game Z", first_release_date = 1704067200L }
            };
            if (Releases)
            {
                var offset = int.Parse(System.Text.RegularExpressions.Regex.Match(body, @"offset (\d+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                var limit = int.Parse(System.Text.RegularExpressions.Regex.Match(body, @"limit (\d+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                return IgdbTest.Json(JsonSerializer.Serialize(rows.Skip(offset).Take(limit)));
            }
            return IgdbTest.Json("""[{"id":10,"name":"Game A","genres":[{"id":3000000012,"name":"RPG"}],"platforms":[{"id":3000000006,"name":"PC"}]}]""");
        }
    }
}
