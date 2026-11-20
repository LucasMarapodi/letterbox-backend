using System.Text.Json;
using Tcc.Api.Contracts;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class ThemeMappingTests
{
    [Fact]
    public async Task OpenAPI_documents_the_required_themes_array_and_nullable_banner()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schema = json.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("GameResponse");
        Assert.Contains("themes", schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        var themes = schema.GetProperty("properties").GetProperty("themes");
        Assert.Equal("array", themes.GetProperty("type").GetString());
        Assert.Contains("IGDB", themes.GetProperty("description").GetString());
        Assert.True(schema.GetProperty("properties").TryGetProperty("bannerUrl", out _));
    }

    [Fact]
    public void Expanded_themes_keep_long_external_ids_deduplicate_trim_and_project_additively()
    {
        var item = Map("""[{"id":3000000040,"name":" Fantasy "},{"id":3000000040,"name":"Fantasy"}]""");
        Assert.Equal("Fantasy", Assert.Single(item.Game.Themes));
        var theme = Assert.Single(item.Game.ThemeEntries);
        Assert.Equal(0, theme.Id);
        Assert.Equal(3_000_000_040, theme.IgdbId);
        using var response = JsonDocument.Parse(JsonSerializer.Serialize(GameResponse.From(item.Game), JsonSerializerOptions.Web));
        Assert.Equal("Fantasy", response.RootElement.GetProperty("themes")[0].GetString());
        Assert.True(response.RootElement.TryGetProperty("genres", out _));
        Assert.Equal(JsonValueKind.Null, response.RootElement.GetProperty("bannerUrl").ValueKind);
    }

    [Theory]
    [InlineData("[{\"id\":0,\"name\":\"Fantasy\"}]")]
    [InlineData("[{\"id\":1,\"name\":\" \"}]")]
    [InlineData("[{\"id\":1,\"name\":\"One\"},{\"id\":1,\"name\":\"Other\"}]")]
    [InlineData("[null]")]
    public void Invalid_themes_are_rejected_as_supplier_data(string themes) =>
        Assert.Throws<CatalogInvalidResponseException>(() => Map(themes));

    [Fact]
    public void Theme_name_is_limited_to_200_characters() =>
        Assert.Throws<CatalogInvalidResponseException>(() => Map("[{\"id\":1,\"name\":\"" + new string('x', 201) + "\"}]"));

    [Fact]
    public void Missing_metadata_differs_from_explicit_empty_and_does_not_remove_genres()
    {
        foreach (var themes in new[] { "null", "[]" })
        {
            var item = Map(themes);
            Assert.Empty(item.Game.Themes);
            Assert.Equal(themes != "null", item.Game.ThemesFetchedAt.HasValue);
            Assert.Equal("RPG", Assert.Single(item.Game.Genres));
        }
    }

    [Fact]
    public async Task Search_and_batch_request_expanded_themes_without_per_card_calls()
    {
        var calls = 0;
        using var fixture = new ExternalFixture(async (request, ct) =>
        {
            calls++;
            var query = await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains("themes.id,themes.name", query);
            return IgdbTest.Json("""[{"id":1,"name":"One","themes":[{"id":40,"name":"Fantasy"}]}]""");
        });
        await fixture.Catalog.SearchAsync(CatalogSearch.Create("One"), default);
        var batch = await fixture.Catalog.ResolveAsync([1, 1], default);
        Assert.Single(batch);
        Assert.Equal(2, calls);
    }

    internal static CatalogGame Map(string themes, long id = 3_000_000_001, DateTimeOffset? fetched = null) =>
        new IgdbGameMapper(ExternalEndpointOptions.Default).Map(JsonSerializer.Deserialize<IgdbGameDto>(
            $$"""{"id":{{id}},"name":"Theme game","genres":[{"id":12,"name":"RPG"}],"themes":{{themes}}} """)!, fetched ?? DateTimeOffset.UtcNow);
}
