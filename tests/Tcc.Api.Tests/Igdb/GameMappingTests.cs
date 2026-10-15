using Tcc.Api.ErrorHandling;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class GameMappingTests
{
    [Fact]
    public void Normalizes_expanded_fields_cover_UTC_date_and_ignores_external_ratings_and_enums()
    {
        const string json = """
            {"id":42,"name":" Game ","summary":"Summary","cover":{"image_id":"co_test","url":"https://untrusted.example"},
             "genres":[{"name":"RPG"},{"name":"RPG"}],"platforms":[{"name":"PC"}],
             "first_release_date":1609459200,"rating":99,"aggregated_rating":90,"game_type":123456,"screenshots":[1]}
            """;
        var dto = JsonSerializer.Deserialize<IgdbGameDto>(json);
        Assert.NotNull(dto);
        var result = IgdbGameMapper.Map(dto, CatalogFlow.Metadata.FetchedAt);
        Assert.Equal(42, result.Game.Id);
        Assert.Equal("Game", result.Game.Title);
        Assert.Equal("Summary", result.Game.Summary);
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big/co_test.jpg", result.Game.CoverUrl);
        Assert.Equal(new DateOnly(2021, 1, 1), result.Game.ReleaseDate);
        Assert.Equal(new[] { "RPG" }, result.Game.Genres);
        Assert.Equal(new[] { "PC" }, result.Game.Platforms);
        Assert.Equal(CatalogFlow.Metadata, result.Metadata);
        Assert.DoesNotContain("rating", JsonSerializer.Serialize(result.Game), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Valid_absent_optional_fields_remain_null_empty_and_outside_domain_metadata()
    {
        var result = IgdbGameMapper.Map(new() { Id = 7, Name = "Minimal" }, CatalogFlow.Metadata.FetchedAt);
        Assert.Equal(7, result.Game.Id);
        Assert.Null(result.Game.CoverUrl);
        Assert.Null(result.Game.ReleaseDate);
        Assert.Empty(result.Game.Genres);
        Assert.Empty(result.Game.Platforms);
        Assert.Equal("", result.Game.Summary);
        Assert.DoesNotContain("FetchedAt", JsonSerializer.Serialize(result.Game));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"id\":0,\"name\":\"Game\"}")]
    [InlineData("{\"id\":-1,\"name\":\"Game\"}")]
    [InlineData("{\"id\":1,\"name\":\" \"}")]
    [InlineData("{\"id\":1,\"name\":\"Game\",\"cover\":{\"image_id\":\"https://untrusted.example/secret\"}}")]
    [InlineData("{\"id\":1,\"name\":\"Game\",\"first_release_date\":9223372036854775807}")]
    public void Invalid_identity_cover_or_date_has_a_safe_integration_error(string json)
    {
        var dto = JsonSerializer.Deserialize<IgdbGameDto>(json);
        Assert.NotNull(dto);
        var failure = Assert.Throws<CatalogInvalidResponseException>(() => IgdbGameMapper.Map(dto, CatalogFlow.Metadata.FetchedAt));
        Assert.DoesNotContain("secret", failure.ToString());
        Assert.Null(failure.InnerException);
    }

    [Fact]
    public async Task Invalid_supplier_payload_is_translated_to_safe_502_by_the_HTTP_pipeline()
    {
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;
        Assert.True(await new ApiExceptionHandler().TryHandleAsync(context, new CatalogInvalidResponseException(), CancellationToken.None));
        Assert.Equal(502, context.Response.StatusCode);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        Assert.Equal("integration_invalid_response", json.RootElement.GetProperty("code").GetString());
    }
}

public sealed class ExternalCatalogTests
{
    [Fact]
    public async Task Resolving_supplier_port_and_empty_batch_do_not_load_credentials_before_external_IO()
    {
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IgdbCredentialOptions>(_ =>
                throw new InvalidOperationException("Credentials must only be read after a supplier miss"))));
        using var scope = host.Services.CreateScope();
        var supplier = scope.ServiceProvider.GetRequiredService<IExternalGameCatalog>();
        Assert.Empty(await supplier.ResolveAsync([], CancellationToken.None));
    }

    [Fact]
    public async Task Search_detail_and_batch_keep_same_id_and_page_lookahead_does_not_invent_total()
    {
        var queries = new List<string>();
        using var fixture = new ExternalFixture(async (request, ct) =>
        {
            Assert.NotNull(request.Content);
            var query = await request.Content.ReadAsStringAsync(ct);
            queries.Add(query);
            return IgdbTest.Json(query.Contains("offset", StringComparison.Ordinal)
                ? "[{\"id\":1,\"name\":\"One\"},{\"id\":2,\"name\":\"Two\"}]" : "[{\"id\":1,\"name\":\"One\"}]");
        });
        var page = await fixture.Catalog.SearchAsync(CatalogSearch.Create("One", pageSize: 1), CancellationToken.None);
        Assert.True(page.HasNextPage);
        Assert.Equal(1, Assert.Single(page.Items).Game.Id);
        var details = await fixture.Catalog.ResolveAsync([1, 1], CancellationToken.None);
        Assert.Equal(page.Items[0].Game, Assert.Single(details.Values).Game);
        Assert.Contains("limit 2; offset 0;", queries[0]);
        Assert.Contains("where id = (1); limit 1;", queries[1]);
        Assert.Equal(CatalogFlow.Metadata.FetchedAt, page.Metadata.FetchedAt);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("not-json-secret")]
    [InlineData("[null]")]
    [InlineData("[{\"id\":\"1\",\"name\":\"Game\"}]")]
    [InlineData("[{\"id\":1,\"name\":\"One\"},{\"id\":1,\"name\":\"Duplicate\"}]")]
    [InlineData("[{\"id\":2,\"name\":\"Unrequested\"}]")]
    public async Task Malformed_duplicate_or_unrequested_supplier_rows_are_not_silently_success(string payload)
    {
        using var fixture = new ExternalFixture((_, _) => Task.FromResult(IgdbTest.Json(payload)));
        var failure = await Assert.ThrowsAsync<CatalogInvalidResponseException>(() => fixture.Catalog.ResolveAsync([1], CancellationToken.None));
        Assert.DoesNotContain("secret", failure.ToString());
    }

    [Fact]
    public async Task Empty_batch_never_acquires_token_or_sends_HTTP_and_empty_supplier_result_is_absence()
    {
        var calls = 0;
        using var fixture = new ExternalFixture((_, _) => { calls++; return Task.FromResult(IgdbTest.Json("[]")); });
        Assert.Empty(await fixture.Catalog.ResolveAsync([], CancellationToken.None));
        Assert.Equal(0, calls);
        Assert.Empty(await fixture.Catalog.ResolveAsync([1], CancellationToken.None));
        Assert.Equal(1, calls);
    }
}

internal sealed class ExternalFixture : IDisposable
{
    private readonly HttpClient oauth;
    private readonly HttpClient http;
    private readonly IgdbAuthenticatedClient client;
    public IgdbGameCatalog Catalog { get; }
    public ExternalFixture(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    {
        var clock = new TestClock { Now = CatalogFlow.Metadata.FetchedAt };
        oauth = new(new TestHandler((_, _) => Task.FromResult(IgdbTest.TokenResponse())));
        var source = new TwitchOAuthClient(oauth, IgdbTest.Options(), clock);
        http = new(new TestHandler(send));
        client = new(http, IgdbTest.Options(), new TwitchTokenProvider(source.AcquireAsync, clock));
        Catalog = new(() => client, clock);
    }
    public void Dispose() { client.Dispose(); http.Dispose(); oauth.Dispose(); }
}
