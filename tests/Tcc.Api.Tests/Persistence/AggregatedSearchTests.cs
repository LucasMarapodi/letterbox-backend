using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Tcc.Application.Catalog;
using Tcc.Domain.Identity;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class AggregatedSearchTests
{
    [Theory]
    [InlineData("")]
    [InlineData("q=ab")]
    [InlineData("q=%20%20ab%20")]
    [InlineData("q=abc&mode=other")]
    [InlineData("q=abc&scope=other")]
    [InlineData("q=abc&page=0")]
    [InlineData("q=abc&page=2")]
    [InlineData("q=abc&mode=results&page=2147483647")]
    [InlineData("q=abc&pageSize=100")]
    [InlineData("q=abc%0Adef")]
    public async Task Invalid_query_returns_400_before_accessing_sources(string query)
    {
        await using var host = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/search?" + query);
        Assert.Equal(400, (int)response.StatusCode);
    }

    [PostgresFact]
    public async Task SQL_people_match_public_names_and_literal_patterns_with_stable_order_and_paging()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        for (var index = 0; index < 31; index++)
            db.Users.Add(new UserAccount(0, "", Email.Create($"private-{index}@example.com"), "synthetic-verifier",
                UserProfile.Create(index == 0 ? "Literal %_ Search" : "Search reader", "description-only-sentinel", updatedAt: DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        using var suggestions = await Read(client, "q=%20SEARCH%20&scope=people");
        Assert.Equal("SEARCH", suggestions.RootElement.GetProperty("query").GetString());
        var group = suggestions.RootElement.GetProperty("people");
        Assert.Equal("ready", group.GetProperty("status").GetString());
        Assert.Equal(5, group.GetProperty("items").GetArrayLength());
        Assert.True(group.GetProperty("hasNextPage").GetBoolean());
        foreach (var person in group.GetProperty("items").EnumerateArray())
            Assert.Equal(new[] { "avatarUrl", "displayName", "id", "username" }, person.EnumerateObject().Select(item => item.Name).Order());
        Assert.Equal("notRequested", suggestions.RootElement.GetProperty("games").GetProperty("status").GetString());
        using var first = await Read(client, "q=search&scope=people&mode=results");
        using var second = await Read(client, "q=search&scope=people&mode=results&page=2");
        var one = first.RootElement.GetProperty("people");
        var two = second.RootElement.GetProperty("people");
        Assert.Equal(20, one.GetProperty("items").GetArrayLength());
        Assert.Equal(11, two.GetProperty("items").GetArrayLength());
        Assert.False(two.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(31, one.GetProperty("items").EnumerateArray().Concat(two.GetProperty("items").EnumerateArray()).Select(item => item.GetProperty("id").GetInt32()).Distinct().Count());
        using var username = await Read(client, "q=%40USER-1&scope=people");
        Assert.Equal(1, username.RootElement.GetProperty("people").GetProperty("items")[0].GetProperty("id").GetInt32());
        foreach (var query in new[] { "private-", "description-only-sentinel" })
        {
            using var absent = await Read(client, "q=" + query + "&scope=people");
            Assert.Equal(0, absent.RootElement.GetProperty("people").GetProperty("items").GetArrayLength());
        }
        using var literal = await Read(client, "q=Literal%20%25_&scope=people");
        Assert.Single(literal.RootElement.GetProperty("people").GetProperty("items").EnumerateArray());
    }

    [PostgresFact]
    public async Task All_scope_uses_real_catalog_pipeline_cache_SQL_and_minimal_game_projection()
    {
        await PostgresCatalogTests.Reset();
        var supplier = new SearchSupplier();
        await using var host = new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IExternalGameCatalog>(supplier)));
        using var client = host.CreateClient();
        using var first = await Read(client, "q=search");
        var group = first.RootElement.GetProperty("games");
        Assert.Equal("ready", group.GetProperty("status").GetString());
        Assert.Equal(5, group.GetProperty("items").GetArrayLength());
        Assert.True(group.GetProperty("hasNextPage").GetBoolean());
        foreach (var game in group.GetProperty("items").EnumerateArray())
        {
            Assert.True(game.GetProperty("id").GetInt32() > 0);
            Assert.Equal(new[] { "coverUrl", "id", "releaseDate", "summary", "title" }, game.EnumerateObject().Select(item => item.Name).Order());
        }
        Assert.False(group.GetProperty("metadata").GetProperty("isStale").GetBoolean());
        using var cached = await Read(client, "q=search");
        Assert.Equal(first.RootElement.GetRawText(), cached.RootElement.GetRawText());
        Assert.Equal(1, supplier.Calls);
        await using var db = PostgresTestDatabase.Context();
        Assert.Equal(5, await db.Games.CountAsync());
        using var peopleOnly = await Read(client, "q=search&scope=people");
        Assert.Equal(1, supplier.Calls);
        Assert.Equal("notRequested", peopleOnly.RootElement.GetProperty("games").GetProperty("status").GetString());
    }

    [PostgresFact]
    public async Task Partial_failure_keeps_people_and_games_only_retry_does_not_query_people()
    {
        await PostgresCatalogTests.Reset();
        var supplier = new PostgresCatalogTests.NoSupplier();
        await using var host = new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IExternalGameCatalog>(supplier)));
        using var client = host.CreateClient();
        using var partial = await Read(client, "q=search");
        Assert.Equal("ready", partial.RootElement.GetProperty("people").GetProperty("status").GetString());
        var group = partial.RootElement.GetProperty("games");
        Assert.Equal("unavailable", group.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, group.GetProperty("page").ValueKind);
        Assert.Equal("integration_unavailable", group.GetProperty("error").GetProperty("code").GetString());
        using var failed = await client.GetAsync("/api/v1/search?q=search&scope=games");
        Assert.Equal(503, (int)failed.StatusCode);
        using var json = JsonDocument.Parse(await failed.Content.ReadAsStringAsync());
        Assert.Equal("notRequested", json.RootElement.GetProperty("people").GetProperty("status").GetString());
        Assert.Equal("search_unavailable", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    internal static async Task<JsonDocument> Read(HttpClient client, string query)
    {
        using var response = await client.GetAsync("/api/v1/search?" + query);
        Assert.Equal(200, (int)response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
    private sealed class SearchSupplier : IExternalGameCatalog
    {
        public int Calls { get; private set; }
        public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken)
        {
            Calls++;
            var items = Enumerable.Range(query.Offset + 1, query.PageSize).Select(index =>
            {
                var item = PostgresCatalogTests.Payload(3_000_000_000L + index);
                return item with { Game = item.Game with { Title = "Search game " + index } };
            }).ToArray();
            return Task.FromResult(new CatalogPage(query, items, true, items[0].Metadata));
        }
        public Task<IReadOnlyDictionary<long, CatalogGame>> ResolveAsync(IReadOnlyList<long> ids, CancellationToken ct) => throw new NotSupportedException();
    }
}
