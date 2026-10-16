using Tcc.Application.Catalog;
using System.Net;
using System.Text.Json;

namespace Tcc.Infrastructure.Igdb;

public sealed partial class IgdbGameCatalog
{
    private async Task<CatalogPage> ReleasePage(CatalogSearch query, CatalogFilterBindings bindings, DateTimeOffset now, CancellationToken ct)
    {
        // Local IDs cannot be used to order unseen provider rows. Complete boundary UTC days,
        // import a bounded window, then let SQL sort by release date/local ID before OFFSET/LIMIT.
        const int maximumRows = 2000;
        long goal = query.PageSize;
        var windowOffset = 0;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var remainingCalls = 20;
        var collected = new List<IgdbGameDto>();
        long? before = null;
        try
        {
            IgdbGameDto[]? initial = await Fetch(query.PageSize + 1, null, offset: query.Offset);
            if (initial.Length == 0) return await releases!.ImportReleasePageAsync(query, [], now, timeout.Token, 0);
            if (query.Offset > 0)
            {
                var firstDay = Day(initial[0]);
                if (--remainingCalls < 0) throw new CatalogIntegrationUnavailableException();
                using var response = await getClient().PostGamesCountAsync(IgdbQueries.NewerReleaseCount(query, now, bindings, firstDay + 86400), timeout.Token);
                if (response.StatusCode != HttpStatusCode.OK) throw new CatalogIntegrationUnavailableException();
                try
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                    if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("count", out var count)
                        || count.ValueKind != JsonValueKind.Number || !count.TryGetInt64(out var newer) || newer < 0 || newer > query.Offset) throw new CatalogInvalidResponseException();
                    windowOffset = (int)(query.Offset - newer);
                }
                catch (JsonException) { throw new CatalogInvalidResponseException(); }
                goal = (long)windowOffset + query.PageSize;
                if (goal >= maximumRows) throw new CatalogIntegrationUnavailableException();
                before = firstDay + 86400;
                initial = null;
            }
            while (true)
            {
                var limit = (int)Math.Min(500, goal + 1 - collected.Count);
                var block = initial ?? await Fetch(limit, before);
                initial = null;
                if (block.Length == 0) break;
                var day = Day(block[^1]);
                var boundary = (int)(goal - collected.Count - 1);
                var completeTail = block.Length == limit && (boundary >= block.Length || Day(block[boundary]) == day);
                var batch = block.ToList();
                if (completeTail)
                {
                    batch.RemoveAll(row => Day(row) == day);
                    long cursor = 0;
                    while (true)
                    {
                        var tied = await Fetch(500, null, day, cursor);
                        foreach (var row in tied)
                        {
                            if (Day(row) != day || row.Id is null || row.Id.Value <= cursor) throw new CatalogInvalidResponseException();
                            cursor = row.Id.Value;
                            batch.Add(row);
                        }
                        if (collected.Count + batch.Count > maximumRows) throw new CatalogIntegrationUnavailableException();
                        if (tied.Length < 500) break;
                    }
                }
                collected.AddRange(batch);
                if (collected.Count > maximumRows) throw new CatalogIntegrationUnavailableException();
                if (collected.Count > goal || block.Length < limit) break;
                before = day;
            }
            if (collected.Select(row => row.Id).Distinct().Count() != collected.Count) throw new CatalogInvalidResponseException();
            var fetched = clock.GetUtcNow();
            var window = collected.Select(row => gameMapper.Map(row, fetched)).ToArray();
            return await releases!.ImportReleasePageAsync(query, window, fetched, timeout.Token, windowOffset);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new CatalogTimeoutException(); }

        async Task<IgdbGameDto[]> Fetch(int limit, long? beforeDay, long? equalDay = null, long afterId = 0, int offset = 0)
        {
            if (--remainingCalls < 0) throw new CatalogIntegrationUnavailableException();
            var rows = await ReadAsync(IgdbQueries.Window(query, now, bindings.GenreId, bindings.PlatformId, limit, offset, beforeDay, equalDay, afterId), timeout.Token);
            ValidateFilters(rows, bindings);
            if (rows.Length > limit) throw new CatalogInvalidResponseException();
            foreach (var row in rows)
                if (row.FirstReleaseDate is null || row.FirstReleaseDate.Value > now.ToUnixTimeSeconds()
                    || beforeDay.HasValue && row.FirstReleaseDate.Value >= beforeDay.Value)
                    throw new CatalogInvalidResponseException();
            if (equalDay is null && !rows.Select(row => row.FirstReleaseDate).SequenceEqual(rows.Select(row => row.FirstReleaseDate).OrderDescending()))
                throw new CatalogInvalidResponseException();
            return rows;
        }
    }

    private static long Day(IgdbGameDto row)
    {
        try
        {
            var utc = DateTimeOffset.FromUnixTimeSeconds(row.FirstReleaseDate!.Value);
            return new DateTimeOffset(utc.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeSeconds();
        }
        catch (ArgumentOutOfRangeException) { throw new CatalogInvalidResponseException(); }
    }
}
