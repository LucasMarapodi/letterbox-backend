using Tcc.Application.Catalog;
using Tcc.Domain.Identity;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class QueryTests
{
    [Fact]
    public void Empty_search_filters_published_releases_and_uses_pagination_with_one_extra_item()
    {
        var now = new TestClock().GetUtcNow();
        var body = IgdbQueries.Search(CatalogSearch.Create(page: 3, pageSize: 20), now);
        Assert.Contains("first_release_date != null & first_release_date <= " + now.ToUnixTimeSeconds(), body);
        Assert.Contains("sort first_release_date desc;", body);
        Assert.Contains("limit 21; offset 40;", body);
        Assert.Contains("fields id,name,summary,cover.image_id,genres.name,platforms.name,first_release_date;", body);
        Assert.DoesNotContain("fields *", body);
        Assert.DoesNotContain("rating", body);
        Assert.DoesNotContain("screenshots", body);
    }

    [Theory]
    [InlineData("name-asc", "name asc")]
    [InlineData("name-desc", "name desc")]
    [InlineData("release-desc", "first_release_date desc")]
    public void Search_has_case_insensitive_infix_and_allowlisted_sort(string sort, string clause)
    {
        var body = IgdbQueries.Search(CatalogSearch.Create("  Aurora  ", sort), new TestClock().GetUtcNow());
        Assert.Contains("where name ~ *\"Aurora\"*", body);
        Assert.Contains("sort " + clause + ";", body);
    }

    [Fact]
    public void Release_sort_with_a_term_also_excludes_future_releases()
    {
        var now = new TestClock().GetUtcNow();
        var body = IgdbQueries.Search(CatalogSearch.Create("Game", "release-desc"), now);
        Assert.Contains("first_release_date <= " + now.ToUnixTimeSeconds(), body);
    }

    [Fact]
    public async Task Adversarial_term_remains_inside_a_literal_and_headers_use_application_token()
    {
        const string term = "A\\B\"; fields *; where id > 0;";
        var query = IgdbQueries.Search(CatalogSearch.Create(term), new TestClock().GetUtcNow());
        Assert.Contains("where name ~ *\"A\\\\B\\\"; fields *; where id > 0;\"*;", query);
        using var oauth = new HttpClient(new TestHandler((_, _) => Task.FromResult(IgdbTest.TokenResponse())));
        var clock = new TestClock();
        var source = new TwitchOAuthClient(oauth, IgdbTest.Options(), clock);
        var tokens = new TwitchTokenProvider(source.AcquireAsync, clock);
        using var http = new HttpClient(new TestHandler(async (request, ct) =>
        {
            Assert.Equal("https://api.igdb.com/v4/games", request.RequestUri?.AbsoluteUri);
            Assert.Equal("synthetic-token", request.Headers.Authorization?.Parameter);
            Assert.Equal("synthetic-id", Assert.Single(request.Headers.GetValues("Client-ID")));
            Assert.NotNull(request.Content);
            Assert.Equal(query, await request.Content.ReadAsStringAsync(ct));
            return IgdbTest.Json("[]");
        }));
        using var client = new IgdbAuthenticatedClient(http, IgdbTest.Options(), tokens);
        using var response = await client.PostGamesAsync(query);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public void Batch_deduplicates_and_uses_only_numeric_ids_with_explicit_limit()
    {
        var body = IgdbQueries.Ids([2, 1, 2]);
        Assert.Contains("where id = (2,1); limit 2;", body);
        Assert.DoesNotContain("igdb-", body);
        Assert.Contains("where id = (9); limit 1;", IgdbQueries.Ids([9]));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Invalid_ids_do_not_build_a_supplier_expression(long id) =>
        Assert.Throws<DomainValidationException>(() => IgdbQueries.Ids([id]));

    [Fact]
    public void Invalid_sort_control_characters_and_oversized_batch_are_rejected()
    {
        Assert.Throws<DomainValidationException>(() => CatalogSearch.Create(sort: "name; fields *"));
        Assert.Throws<DomainValidationException>(() => CatalogSearch.Create("a\nb"));
        Assert.Throws<DomainValidationException>(() => IgdbQueries.Ids(Enumerable.Range(1, 501).Select(i => (long)i).ToArray()));
    }
}
