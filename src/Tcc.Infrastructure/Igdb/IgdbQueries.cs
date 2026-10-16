using Tcc.Application.Catalog;
using System.Globalization;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Igdb;

public static class IgdbQueries
{
    private const string Fields = "fields id,name,summary,cover.image_id,genres.id,genres.name,platforms.id,platforms.name,first_release_date; ";
    public static string Search(CatalogSearch query, DateTimeOffset now, long? genreId = null, long? platformId = null) =>
        Window(query, now, genreId, platformId, query.PageSize + 1, query.Offset);
    internal static string Window(CatalogSearch query, DateTimeOffset now, long? genreId, long? platformId, int limit, int offset,
        long? before = null, long? equal = null, long afterId = 0)
    {
        var filter = Filter(query, now, genreId, platformId);
        var sort = query.Sort switch { "name-asc" => "name asc", "name-desc" => "name desc", _ => "first_release_date desc" };
        if (before.HasValue) filter += " & first_release_date < " + before.Value.ToString(CultureInfo.InvariantCulture);
        if (equal.HasValue) { filter += " & first_release_date >= " + equal.Value.ToString(CultureInfo.InvariantCulture)
            + " & first_release_date < " + (equal.Value + 86400).ToString(CultureInfo.InvariantCulture)
            + " & id > " + afterId.ToString(CultureInfo.InvariantCulture); sort = "id asc"; }
        return Fields + "where " + filter + "; sort " + sort + "; limit "
            + limit.ToString(CultureInfo.InvariantCulture) + "; offset " + offset.ToString(CultureInfo.InvariantCulture) + ";";
    }
    internal static string NewerReleaseCount(CatalogSearch query, DateTimeOffset now, CatalogFilterBindings bindings, long afterDay) =>
        "where " + Filter(query, now, bindings.GenreId, bindings.PlatformId) + " & first_release_date >= " + afterDay.ToString(CultureInfo.InvariantCulture) + ";";
    private static string Filter(CatalogSearch query, DateTimeOffset now, long? genreId, long? platformId)
    {
        var term = query.Term.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        var filter = query.Term.Length == 0
            ? "first_release_date != null & first_release_date <= " + now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
            : "name ~ *\"" + term + "\"*";
        if (query.Term.Length > 0 && query.Sort == "release-desc") filter += " & first_release_date != null & first_release_date <= "
            + now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        if (query.GenreId.HasValue && genreId is not > 0 || query.PlatformId.HasValue && platformId is not > 0) throw new InvalidCatalogFilterException();
        if (genreId.HasValue) filter += " & genres = (" + genreId.Value.ToString(CultureInfo.InvariantCulture) + ")";
        if (platformId.HasValue) filter += " & platforms = (" + platformId.Value.ToString(CultureInfo.InvariantCulture) + ")";
        return filter;
    }
    public static string Ids(IReadOnlyList<long> ids)
    {
        var unique = ExternalCatalogIds.Unique(ids);
        if (unique.Length == 0) throw new DomainValidationException("Lote vazio não requer consulta externa.");
        return Fields + "where id = (" + string.Join(',', unique.Select(id => id.ToString(CultureInfo.InvariantCulture)))
            + "); limit " + unique.Length.ToString(CultureInfo.InvariantCulture) + ";";
    }
}
