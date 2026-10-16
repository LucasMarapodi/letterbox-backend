using Tcc.Api.ErrorHandling;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;

namespace Tcc.Api.Tests.Igdb;

public sealed class CatalogAvailabilityTests
{
    [Fact]
    public async Task Failed_query_uses_stale_cache_before_local_without_resetting_fetchedAt()
    {
        var flow = new CatalogFlow();
        var query = CatalogSearch.Create("Game");
        var cached = new CatalogPage(query, [CatalogFlow.Game("igdb-1")], true, CatalogFlow.Metadata with { IsStale = true });
        var local = cached with { Items = [CatalogFlow.Game("igdb-2")] };
        flow.Cache.Page = cached; flow.Local.Page = local;
        var source = new FailingSupplier(new CatalogIntegrationUnavailableException());
        var catalog = new AsyncGameCatalog(flow.Cache, flow.Local, source);
        var result = await catalog.SearchAsync(query);
        Assert.Equal("igdb-1", Assert.Single(result.Items).Game.Id);
        Assert.True(result.Metadata.IsStale);
        Assert.Equal(CatalogFlow.Metadata.FetchedAt, result.Metadata.FetchedAt);
        Assert.DoesNotContain("cache.save-page", flow.Events);
        Assert.DoesNotContain("local.save-page", flow.Events);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(65, true)]
    [InlineData(66, false)]
    public async Task Local_query_fallback_is_limited_to_sixty_minutes_after_freshness(int ageMinutes, bool allowed)
    {
        var clock = new TestClock { Now = CatalogFlow.Metadata.FetchedAt };
        var options = new CatalogMemoryOptions();
        var cache = new InMemoryCatalogCache(clock, options);
        var local = new InMemoryLocalGameCatalog(clock, options);
        var query = CatalogSearch.Create("Game");
        await local.SavePageAsync(new(query, [CatalogFlow.Game("igdb-1")], false, CatalogFlow.Metadata), default);
        clock.Now += TimeSpan.FromMinutes(ageMinutes);
        var catalog = new AsyncGameCatalog(cache, local, new FailingSupplier(new CatalogIntegrationUnavailableException()));
        if (allowed)
        {
            var page = await catalog.SearchAsync(query);
            Assert.True(page.Metadata.IsStale);
            Assert.Equal(CatalogFlow.Metadata.FetchedAt, page.Metadata.FetchedAt);
        }
        else await Assert.ThrowsAsync<CatalogIntegrationUnavailableException>(() => catalog.SearchAsync(query));
        Assert.NotEmpty(await local.FindGamesAsync(["igdb-1"], default));
    }

    [Fact]
    public async Task Timeout_can_use_local_snapshot_but_invalid_payload_is_not_hidden_by_stale()
    {
        var flow = new CatalogFlow();
        var query = CatalogSearch.Create();
        flow.Local.Page = new(query, [], false, CatalogFlow.Metadata with { IsStale = true });
        var timeout = new AsyncGameCatalog(flow.Cache, flow.Local, new FailingSupplier(new CatalogTimeoutException()));
        Assert.True((await timeout.SearchAsync(query)).Metadata.IsStale);
        var malformed = new AsyncGameCatalog(flow.Cache, flow.Local, new FailingSupplier(new CatalogInvalidResponseException()));
        await Assert.ThrowsAsync<CatalogInvalidResponseException>(() => malformed.SearchAsync(query));
    }

    [Fact]
    public async Task Known_local_detail_remains_readable_offline_with_original_timestamp()
    {
        var clock = new TestClock { Now = CatalogFlow.Metadata.FetchedAt.AddHours(2) };
        var options = new CatalogMemoryOptions();
        var local = new InMemoryLocalGameCatalog(clock, options);
        await local.SaveGamesAsync([CatalogFlow.Game("igdb-1")], default);
        var source = new FailingSupplier(new CatalogIntegrationUnavailableException());
        var catalog = new AsyncGameCatalog(new InMemoryCatalogCache(clock, options), local, source);
        var game = await catalog.GetAsync("igdb-1");
        Assert.NotNull(game);
        Assert.True(game.Metadata.IsStale);
        Assert.Equal(CatalogFlow.Metadata.FetchedAt, game.Metadata.FetchedAt);
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task Failed_explicit_refresh_keeps_local_data_and_confirmed_absence_does_not_delete_it()
    {
        var clock = new TestClock { Now = CatalogFlow.Metadata.FetchedAt.AddHours(2) };
        var options = new CatalogMemoryOptions();
        var local = new InMemoryLocalGameCatalog(clock, options);
        var cache = new InMemoryCatalogCache(clock, options);
        await local.SaveGamesAsync([CatalogFlow.Game("igdb-1")], default);
        var offline = new AsyncGameCatalog(cache, local, new FailingSupplier(new CatalogTimeoutException()));
        var fallback = await offline.RefreshAsync("igdb-1");
        Assert.NotNull(fallback);
        Assert.True(fallback.Metadata.IsStale);
        Assert.Equal(CatalogFlow.Metadata.FetchedAt, fallback.Metadata.FetchedAt);
        var empty = new CatalogFlow();
        var absent = new AsyncGameCatalog(cache, local, empty.External);
        Assert.Null(await absent.RefreshAsync("igdb-1"));
        Assert.NotEmpty(await local.FindGamesAsync(["igdb-1"], default));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    public async Task Supplier_rejection_is_not_transient_or_a_successful_empty_catalog(int status)
    {
        using var fixture = new ExternalFixture((_, _) => Task.FromResult(IgdbTest.Json("supplier-body-secret", (HttpStatusCode)status)));
        var error = await Assert.ThrowsAsync<CatalogInvalidResponseException>(() => fixture.Catalog.SearchAsync(CatalogSearch.Create("Game"), default));
        Assert.DoesNotContain("secret", error.ToString());
    }

    [Fact]
    public async Task Persistent_supplier_401_is_not_eligible_for_stale_query_fallback()
    {
        using var fixture = new ExternalFixture((_, _) => Task.FromResult(IgdbTest.Json("[]", HttpStatusCode.Unauthorized)));
        var error = await Assert.ThrowsAsync<ExternalIntegrationUnavailableException>(() => fixture.Catalog.SearchAsync(CatalogSearch.Create("Game"), default));
        Assert.False(error.AllowsStaleFallback);
    }

    [Fact]
    public async Task Timeout_is_a_safe_504_and_not_platform_401()
    {
        var context = new DefaultHttpContext();
        using var stream = new MemoryStream(); context.Response.Body = stream;
        Assert.True(await new ApiExceptionHandler().TryHandleAsync(context, new CatalogTimeoutException(), default));
        Assert.Equal(504, context.Response.StatusCode);
        stream.Position = 0;
        using var json = await JsonDocument.ParseAsync(stream);
        Assert.Equal("integration_timeout", json.RootElement.GetProperty("code").GetString());
    }
}

internal sealed class FailingSupplier(Exception failure) : IExternalGameCatalog
{
    public int Calls;
    public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken)
    { Calls++; return Task.FromException<CatalogPage>(failure); }
    public Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    { Calls++; return Task.FromException<IReadOnlyDictionary<string, CatalogGame>>(failure); }
}
