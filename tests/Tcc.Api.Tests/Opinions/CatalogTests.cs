using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Opinions;

public sealed class CatalogTests
{
    [Fact]
    public void Fake_adapter_is_constructible_without_network_and_infrastructure_has_no_http_reference()
    {
        var adapter = new Tcc.Infrastructure.Catalog.FakeIgdbCatalog();
        Assert.Equal(3, adapter.All().Count);
        Assert.DoesNotContain(typeof(Tcc.Infrastructure.Catalog.FakeIgdbCatalog).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "System.Net.Http");
    }

    [Fact]
    public async Task Public_search_trims_and_sorts_in_both_directions()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        var ascending = await client.GetFromJsonAsync<GameDto[]>("/api/v1/games");
        var descending = await client.GetFromJsonAsync<GameDto[]>("/api/v1/games?sort=name-desc");
        Assert.NotNull(ascending);
        Assert.NotNull(descending);
        Assert.Equal(new[] { "game-3", "game-2", "game-1" }, ascending.Select(game => game.Id));
        Assert.Equal(ascending.Select(game => game.Id).Reverse(), descending.Select(game => game.Id));
        var filtered = await client.GetFromJsonAsync<GameDto[]>("/api/v1/games?term=%20AURORA%20");
        Assert.NotNull(filtered);
        Assert.Equal("game-2", Assert.Single(filtered).Id);
        Assert.Empty((await client.GetFromJsonAsync<GameDto[]>("/api/v1/games?term=missing")) ?? []);
    }

    [Fact]
    public async Task Details_preserve_normalized_data_and_nulls()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        var game = await client.GetFromJsonAsync<GameDto>("/api/v1/games/game-3");
        Assert.NotNull(game);
        Assert.Equal("Atlas de Cinzas", game.Title);
        Assert.Null(game.CoverUrl);
        Assert.Null(game.ReleaseDate);
        Assert.Null(game.AverageRating);
        Assert.Contains("RPG", game.Genres);
        Assert.Contains("PC", game.Platforms);
    }

    [Theory]
    [InlineData("/api/v1/games/missing", 404)]
    [InlineData("/api/v1/games?sort=invalid", 400)]
    public async Task Invalid_queries_have_expected_status(string url, int status)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.GetAsync(url);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
    }
}
