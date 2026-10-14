using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Igdb;

public static class IgdbQueries
{
    public static string Search(CatalogSearch query, DateTimeOffset now) { _ = query; _ = now; return ""; }
    public static string Ids(IReadOnlyList<string> ids) { _ = ids; return ""; }
}
