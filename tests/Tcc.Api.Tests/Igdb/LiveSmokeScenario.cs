using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Text.Json;
using Tcc.Api.Contracts;
using Tcc.Infrastructure.Igdb;
using Tcc.Application.Catalog;

namespace Tcc.Api.Tests.Igdb;

internal sealed record SmokeStep(string Name, int Status, int OAuthRequests, int IgdbRequests, string? GameId = null,
    bool? Stale = null, DateTimeOffset? FetchedAt = null);
internal sealed record SmokeReport(string Mode, string Result, string Reason, DateTimeOffset At, int OAuthRequests,
    int IgdbRequests, IReadOnlyList<SmokeStep> Steps);
internal sealed class SmokeCounters
{
    private int oauth;
    private int igdb;
    public int OAuthRequests => Volatile.Read(ref oauth);
    public int IgdbRequests => Volatile.Read(ref igdb);
    public void Record(bool oauth)
    {
        if (oauth) Increment(ref this.oauth, 2); else Increment(ref igdb, 6);
    }
    private static void Increment(ref int count, int limit)
    {
        while (true)
        {
            var previous = Volatile.Read(ref count);
            if (previous >= limit) throw new HttpRequestException("Orçamento do smoke esgotado.");
            if (Interlocked.CompareExchange(ref count, previous + 1, previous) == previous) return;
        }
    }
}
internal static class LiveSmokeScenario
{
    public static async Task<SmokeReport> RunAsync(WebApplicationFactory<Program> host, SmokeCounters counters,
        bool requested, bool credentialsReady, string mode, CancellationToken cancellationToken = default)
    {
        var steps = new List<SmokeStep>();
        SmokeReport Report(string result, string reason) => new(mode, result, reason, DateTimeOffset.UtcNow,
            counters.OAuthRequests, counters.IgdbRequests, steps.AsReadOnly());
        if (!requested) return Report("DISABLED", "opt_in_required");
        if (!credentialsReady) return Report("BLOCKED", "credentials_missing_or_placeholder");
        try
        {
            using var client = host.CreateClient();
            var options = host.Services.GetRequiredService<IgdbCredentialOptions>();
            var snapshots = new List<string>();
            async Task<(string Body, CatalogMetadata Metadata)> Get(string path, string name, string? gameId = null)
            {
                using var response = await client.GetAsync(path, cancellationToken);
                var status = (int)response.StatusCode;
                if (status != 200)
                { steps.Add(new(name, status, counters.OAuthRequests, counters.IgdbRequests, gameId)); throw new SmokeFailure("catalog_http_" + status); }
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                snapshots.Add(body + response.Headers + response.Content.Headers);
                if (!response.Headers.TryGetValues("X-Catalog-Stale", out var staleValues)
                    || !bool.TryParse(staleValues.SingleOrDefault(), out var stale)
                    || !response.Headers.TryGetValues("X-Catalog-Fetched-At", out var fetchedValues)
                    || !DateTimeOffset.TryParse(fetchedValues.SingleOrDefault(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fetched))
                    throw new SmokeFailure("catalog_metadata_missing");
                steps.Add(new(name, status, counters.OAuthRequests, counters.IgdbRequests, gameId, stale, fetched));
                if (stale) throw new SmokeFailure("unexpected_stale_in_fresh_host");
                return (body, new(fetched, stale));
            }
            var first = await Get("/api/v1/games?pageSize=2", "recent_page");
            var page = JsonSerializer.Deserialize<GamePageResponse>(first.Body, JsonSerializerOptions.Web) ?? throw new SmokeFailure("invalid_page");
            if (page.Items.Count == 0 || page.Page != 1 || page.PageSize != 2) throw new SmokeFailure("empty_or_invalid_page");
            if (page.Items.Any(game => game.ReleaseDate is null || game.ReleaseDate > DateOnly.FromDateTime(DateTime.UtcNow)))
                throw new SmokeFailure("future_or_undated_recent_game");
            var calls = (counters.OAuthRequests, counters.IgdbRequests);
            var repeat = await Get("/api/v1/games?pageSize=2", "cache_hit");
            if (repeat.Body != first.Body || repeat.Metadata != first.Metadata || calls != (counters.OAuthRequests, counters.IgdbRequests))
                throw new SmokeFailure("cache_not_reused");
            var id = page.Items[0].Id;
            CatalogIds.Parse(id);
            var detail = await Get("/api/v1/games/" + id, "detail", id);
            var game = JsonSerializer.Deserialize<GameResponse>(detail.Body, JsonSerializerOptions.Web) ?? throw new SmokeFailure("invalid_detail");
            if (game.Id != id || game.AverageRating is not null || calls != (counters.OAuthRequests, counters.IgdbRequests))
                throw new SmokeFailure("detail_id_average_or_cache_invalid");
            var cache = host.Services.GetRequiredService<ICatalogCache>() as InMemoryCatalogCache ?? throw new SmokeFailure("unexpected_cache");
            if (!cache.EvictGame(id) || (await cache.FindGamesAsync([id], cancellationToken)).Count != 0)
                throw new SmokeFailure("targeted_eviction_failed");
            if ((await host.Services.GetRequiredService<ILocalGameCatalog>().FindGamesAsync([id], cancellationToken)).Count != 1)
                throw new SmokeFailure("known_local_game_missing");
            var local = await Get("/api/v1/games/" + id, "local_hit_after_eviction", id);
            if (local.Body != detail.Body || local.Metadata != detail.Metadata || calls != (counters.OAuthRequests, counters.IgdbRequests))
                throw new SmokeFailure("local_lookup_changed_data_or_called_supplier");
            if (page.HasNextPage)
            {
                var next = await Get("/api/v1/games?page=2&pageSize=2", "next_page");
                var nextPage = JsonSerializer.Deserialize<GamePageResponse>(next.Body, JsonSerializerOptions.Web);
                if (nextPage is null || nextPage.Page != 2 || nextPage.PageSize != 2) throw new SmokeFailure("invalid_next_page");
            }
            await Get("/api/v1/games?term=Zelda&pageSize=2", "term_search");
            var token = await host.Services.GetRequiredService<TwitchTokenProvider>().GetAsync(cancellationToken);
            if (counters.OAuthRequests != calls.OAuthRequests) throw new SmokeFailure("token_not_reused");
            snapshots.Add(await client.GetStringAsync("/openapi/v1.json", cancellationToken));
            if (snapshots.Any(snapshot => new[] { options.ClientId, options.ClientSecret, token.Value }.Any(value => snapshot.Contains(value, StringComparison.Ordinal))))
                throw new SmokeFailure("credential_detected_in_public_artifact");
            return Report("PASSED", "catalog_cache_local_token_and_search_verified");
        }
        catch (SmokeFailure failure) { return Report("FAILED", failure.Code); }
        catch (OperationCanceledException) { return Report("FAILED", "timeout_or_cancelled"); }
        catch (Exception) { return Report("FAILED", "safe_unexpected_failure"); }
    }
    private sealed class SmokeFailure(string code) : Exception("Smoke rejeitado.") { public string Code { get; } = code; }
}
