using Tcc.Application.Catalog;
using System.Text.Json;
using System.Net;

namespace Tcc.Infrastructure.Igdb;

public sealed class IgdbGameCatalog(Func<IgdbAuthenticatedClient> getClient, TimeProvider clock) : IExternalGameCatalog
{
    public async Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var rows = await ReadAsync(IgdbQueries.Search(query, now), cancellationToken);
        if (rows.Length > query.PageSize + 1 || ((query.Term.Length == 0 || query.Sort == "release-desc")
            && rows.Any(row => row.FirstReleaseDate is null || row.FirstReleaseDate > now.ToUnixTimeSeconds())))
            throw new CatalogInvalidResponseException();
        var fetchedAt = clock.GetUtcNow();
        var games = rows.Select(row => IgdbGameMapper.Map(row, fetchedAt)).ToArray();
        return new(query, Array.AsReadOnly(games.Take(query.PageSize).ToArray()), games.Length > query.PageSize, new(fetchedAt));
    }
    public async Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unique = CatalogIds.Unique(ids);
        if (unique.Length == 0) return new Dictionary<string, CatalogGame>();
        var rows = await ReadAsync(IgdbQueries.Ids(unique), cancellationToken);
        var fetchedAt = clock.GetUtcNow();
        var games = rows.Select(row => IgdbGameMapper.Map(row, fetchedAt)).ToArray();
        if (games.Any(game => !unique.Contains(game.Game.Id, StringComparer.Ordinal))) throw new CatalogInvalidResponseException();
        return games.ToDictionary(game => game.Game.Id, StringComparer.Ordinal);
    }
    private async Task<IgdbGameDto[]> ReadAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var response = await getClient().PostGamesAsync(query, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK) throw new ExternalIntegrationUnavailableException();
        try
        {
            var rows = JsonSerializer.Deserialize<IgdbGameDto[]>(await response.Content.ReadAsStringAsync(cancellationToken));
            if (rows is null || rows.Any(row => row is null) || rows.Select(row => row.Id).Distinct().Count() != rows.Length)
                throw new CatalogInvalidResponseException();
            return rows;
        }
        catch (JsonException) { throw new CatalogInvalidResponseException(); }
    }
}
