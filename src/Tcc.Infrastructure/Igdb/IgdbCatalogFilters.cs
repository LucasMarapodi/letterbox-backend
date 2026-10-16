using System.Globalization;
using System.Net;
using System.Text.Json;
using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Igdb;

public sealed class IgdbCatalogFilters(Func<IgdbAuthenticatedClient> getClient) : IExternalCatalogFilters
{
    public async Task<ExternalCatalogFilters> ReadAsync(CancellationToken ct) => new(await Read(true, ct), await Read(false, ct));
    private async Task<IReadOnlyList<ExternalCatalogFilter>> Read(bool genres, CancellationToken ct)
    {
        var result = new List<ExternalCatalogFilter>();
        long cursor = 0;
        for (var page = 0; page < 20; page++)
        {
            var query = "fields id,name; where id > " + cursor.ToString(CultureInfo.InvariantCulture) + "; sort id asc; limit 500;";
            using var response = genres ? await getClient().PostGenresAsync(query, ct) : await getClient().PostPlatformsAsync(query, ct);
            if (response.StatusCode != HttpStatusCode.OK) throw new CatalogIntegrationUnavailableException();
            IgdbNameDto[]? rows;
            try { rows = JsonSerializer.Deserialize<IgdbNameDto[]>(await response.Content.ReadAsStringAsync(ct)); }
            catch (JsonException) { throw new CatalogInvalidResponseException(); }
            if (rows is null || rows.Length > 500) throw new CatalogInvalidResponseException();
            foreach (var row in rows)
            {
                if (row is null || row.Id is null || row.Id.Value <= cursor || string.IsNullOrWhiteSpace(row.Name) || row.Name.Any(char.IsControl))
                    throw new CatalogInvalidResponseException();
                cursor = row.Id.Value;
                result.Add(new(cursor, row.Name.Trim()));
            }
            if (rows.Length < 500) return result.AsReadOnly();
        }
        throw new CatalogIntegrationUnavailableException();
    }
}
