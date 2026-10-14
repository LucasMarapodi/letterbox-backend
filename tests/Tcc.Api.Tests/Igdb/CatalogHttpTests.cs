using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Application.Catalog;

namespace Tcc.Api.Tests.Igdb;

public sealed class CatalogHttpTests
{
    [Theory]
    [InlineData("", "release-desc", 1, 20)]
    [InlineData("?term=Atlas&page=2&pageSize=3", "name-asc", 2, 3)]
    public async Task List_uses_async_catalog_and_pagination(string query, string sort, int page, int size)
    {
        var source = new HttpCatalog();
        await using var host = Host(source);
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/games" + query);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        Assert.Equal(page, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(size, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.False(json.RootElement.GetProperty("hasNextPage").GetBoolean());
        Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(sort, source.Query?.Sort);
        Assert.Equal("false", Assert.Single(response.Headers.GetValues("X-Catalog-Stale")));
        Assert.Contains("2026-10-14", Assert.Single(response.Headers.GetValues("X-Catalog-Fetched-At")));
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?sort=raw-query")]
    public async Task Invalid_query_is_rejected_before_supplier(string query)
    {
        var source = new HttpCatalog();
        await using var host = Host(source);
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/games" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(source.Query);
    }

    [Theory]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task Supplier_failure_has_own_status_and_no_success_metadata(int status)
    {
        var source = new HttpCatalog { Failure = status };
        await using var host = Host(source);
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/games");
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Catalog-Stale"));
    }

    [Fact]
    public async Task Cors_exposes_metadata_and_detail_preserves_contract()
    {
        await using var host = Host(new HttpCatalog());
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games/igdb-1");
        request.Headers.Add("Origin", "http://localhost:5173");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Contains("X-Catalog-Stale", string.Join(',', response.Headers.GetValues("Access-Control-Expose-Headers")));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("igdb-1", json.RootElement.GetProperty("id").GetString());
    }

    internal static WebApplicationFactory<Program> Host(IAsyncGameCatalog source) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.UseEnvironment("Development")
            .ConfigureServices(services => services.AddSingleton(source)));
}

internal sealed class HttpCatalog : IAsyncGameCatalog
{
    public CatalogSearch? Query { get; private set; }
    public int Failure { get; init; }
    public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken = default)
    {
        Query = query;
        if (Failure != 0) throw Failure switch
        { 502 => new CatalogInvalidResponseException(), 504 => new CatalogTimeoutException(), _ => new CatalogIntegrationUnavailableException() };
        return Task.FromResult(new CatalogPage(query, [CatalogFlow.Game("igdb-1")], false, CatalogFlow.Metadata));
    }
    public Task<CatalogGame?> GetAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<CatalogGame?>(CatalogFlow.Game(id));
    public Task<CatalogGame?> RefreshAsync(string id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);
    public Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, CatalogGame>>(ids.Distinct().ToDictionary(id => id, CatalogFlow.Game));
}
