using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Opinions;

public sealed class CatalogTests
{
    [Fact]
    public void Fake_adapter_is_constructible_without_network_and_inner_layers_have_no_http_reference()
    {
        var adapter = FixtureCatalog.Games;
        Assert.Equal(3, adapter.Length);
        Assert.DoesNotContain(typeof(Tcc.Domain.Catalog.Game).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "System.Net.Http");
        Assert.DoesNotContain(typeof(Tcc.Application.Catalog.IAsyncGameCatalog).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "System.Net.Http");
    }

    [Fact]
    public async Task Public_search_trims_and_sorts_in_both_directions()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        var ascending = await client.GetFromJsonAsync<GamePageDto>("/api/v1/games?sort=name-asc");
        var descending = await client.GetFromJsonAsync<GamePageDto>("/api/v1/games?sort=name-desc");
        Assert.NotNull(ascending);
        Assert.NotNull(descending);
        Assert.Equal(new[] { "igdb-3", "igdb-2", "igdb-1" }, ascending.Items.Select(game => game.Id));
        Assert.Equal(ascending.Items.Select(game => game.Id).Reverse(), descending.Items.Select(game => game.Id));
        var filtered = await client.GetFromJsonAsync<GamePageDto>("/api/v1/games?term=%20AURORA%20");
        Assert.NotNull(filtered);
        Assert.Equal("igdb-2", Assert.Single(filtered.Items).Id);
        Assert.Empty((await client.GetFromJsonAsync<GamePageDto>("/api/v1/games?term=missing"))?.Items ?? []);
    }

    [Fact]
    public async Task Details_preserve_normalized_data_and_nulls()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        var game = await client.GetFromJsonAsync<GameDto>("/api/v1/games/igdb-3");
        Assert.NotNull(game);
        Assert.Equal("Atlas de Cinzas", game.Title);
        Assert.Null(game.CoverUrl);
        Assert.Null(game.ReleaseDate);
        Assert.Null(game.AverageRating);
        Assert.Contains("RPG", game.Genres);
        Assert.Contains("PC", game.Platforms);
    }

    [Theory]
    [InlineData("/api/v1/games/igdb-999", 404)]
    [InlineData("/api/v1/games?sort=invalid", 400)]
    public async Task Invalid_queries_have_expected_status(string url, int status)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var response = await client.GetAsync(url);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
    }
}


public sealed record GamePageDto(GameDto[] Items, int Page, int PageSize, bool HasNextPage);
