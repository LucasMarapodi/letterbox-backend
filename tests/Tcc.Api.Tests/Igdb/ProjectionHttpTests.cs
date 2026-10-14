using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Application.Catalog;
using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;

namespace Tcc.Api.Tests.Igdb;

public sealed class ProjectionHttpTests
{
    [Fact]
    public async Task Recent_reviews_resolve_unique_games_in_one_batch_with_oldest_stale_metadata()
    {
        var catalog = new BatchCatalog();
        await using var host = CatalogHttpTests.Host(catalog);
        var reviews = host.Services.GetRequiredService<IReviews>();
        reviews.TryAdd(Review.Create("a", "user-1", "igdb-10", "A", CatalogFlow.Metadata.FetchedAt));
        // Use an existing author for every projection; duplicate games across separate review IDs.
        reviews.TryAdd(Review.Create("c", "user-1", "igdb-11", "C", CatalogFlow.Metadata.FetchedAt));
        using var client = host.CreateClient();
        // A second author is registered locally, with no external request.
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "test"));
        registration.EnsureSuccessStatusCode();
        // Replace b with a valid local author's ID by using the account port.
        var accounts = host.Services.GetRequiredService<Tcc.Application.Identity.IUserAccounts>();
        var second = accounts.FindByEmail(Tcc.Domain.Identity.Email.Create("b@example.com")) ?? throw new InvalidOperationException();
        reviews.TryAdd(Review.Create("d", second.Id, "igdb-10", "D", CatalogFlow.Metadata.FetchedAt));
        using var response = await client.GetAsync("/api/v1/community/reviews/recent");
        response.EnsureSuccessStatusCode();
        Assert.Single(catalog.Batches);
        Assert.Equal(new[] { "igdb-10", "igdb-11" }, catalog.Batches[0].Where(id => id.StartsWith("igdb-", StringComparison.Ordinal)).Order());
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("X-Catalog-Stale")));
        Assert.Contains("2026-10-13", Assert.Single(response.Headers.GetValues("X-Catalog-Fetched-At")));
    }

    [Fact]
    public async Task Known_local_review_is_readable_with_empty_cache_and_unavailable_supplier()
    {
        var external = new OfflineCatalog();
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IExternalGameCatalog>(external)));
        await host.Services.GetRequiredService<ILocalGameCatalog>().SaveGamesAsync([
            CatalogFlow.Game("igdb-10") with { Metadata = new(CatalogFlow.Metadata.FetchedAt.AddDays(-2), true) }], default);
        var reviews = host.Services.GetRequiredService<IReviews>();
        reviews.TryAdd(Review.Create("known", "user-1", "igdb-10", "Keep me", CatalogFlow.Metadata.FetchedAt));
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/reviews/known");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, external.Calls);
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("X-Catalog-Stale")));
        Assert.Equal("Keep me", reviews.Find("known")?.Text);
    }

    [Fact]
    public async Task Comments_do_not_resolve_catalog_or_emit_catalog_headers()
    {
        var catalog = new BatchCatalog();
        await using var host = CatalogHttpTests.Host(catalog);
        host.Services.GetRequiredService<IReviews>().TryAdd(Review.Create("known", "user-1", "igdb-10", "Keep", CatalogFlow.Metadata.FetchedAt));
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/reviews/known/comments");
        response.EnsureSuccessStatusCode();
        Assert.Empty(catalog.Batches);
        Assert.False(response.Headers.Contains("X-Catalog-Stale"));
    }
}

internal sealed class BatchCatalog : IAsyncGameCatalog
{
    public List<string[]> Batches { get; } = [];
    public Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        Batches.Add(ids.ToArray());
        return Task.FromResult<IReadOnlyDictionary<string, CatalogGame>>(ids.Distinct().ToDictionary(id => id, id =>
            CatalogFlow.Game(id) with { Metadata = id == "igdb-10" ? new(CatalogFlow.Metadata.FetchedAt.AddDays(-1), true) : CatalogFlow.Metadata }));
    }
    public async Task<CatalogGame?> GetAsync(string id, CancellationToken cancellationToken = default) => (await ResolveAsync([id], cancellationToken))[id];
    public Task<CatalogGame?> RefreshAsync(string id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);
    public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
internal sealed class OfflineCatalog : IExternalGameCatalog
{
    public int Calls { get; private set; }
    public Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    { Calls++; throw new CatalogIntegrationUnavailableException(); }
    public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken)
    { Calls++; throw new CatalogIntegrationUnavailableException(); }
}
