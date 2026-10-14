using Tcc.Application.Catalog;
using System.Globalization;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Igdb;

public static class IgdbQueries
{
    private const string Fields = "fields id,name,summary,cover.image_id,genres.name,platforms.name,first_release_date; ";
    public static string Search(CatalogSearch query, DateTimeOffset now)
    {
        var term = query.Term.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        var filter = query.Term.Length == 0
            ? "first_release_date != null & first_release_date <= " + now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
            : "name ~ *\"" + term + "\"*";
        if (query.Term.Length > 0 && query.Sort == "release-desc") filter += " & first_release_date != null";
        var sort = query.Sort switch { "name-asc" => "name asc", "name-desc" => "name desc", _ => "first_release_date desc" };
        return Fields + "where " + filter + "; sort " + sort + "; limit "
            + (query.PageSize + 1).ToString(CultureInfo.InvariantCulture) + "; offset " + query.Offset.ToString(CultureInfo.InvariantCulture) + ";";
    }
    public static string Ids(IReadOnlyList<string> ids)
    {
        var unique = CatalogIds.Unique(ids);
        if (unique.Length == 0) throw new DomainValidationException("Lote vazio não requer consulta externa.");
        return Fields + "where id = (" + string.Join(',', unique.Select(id => CatalogIds.Parse(id).ToString(CultureInfo.InvariantCulture)))
            + "); limit " + unique.Length.ToString(CultureInfo.InvariantCulture) + ";";
    }
}
