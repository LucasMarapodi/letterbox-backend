namespace Tcc.Application.Catalog;

// Scoped to one use case/request; records only entries actually used in its response.
public sealed class CatalogUsage
{
    public CatalogMetadata? Metadata { get; private set; }
    public void Include(CatalogMetadata entry) => Metadata = Metadata is null ? entry :
        new(Metadata.FetchedAt < entry.FetchedAt ? Metadata.FetchedAt : entry.FetchedAt,
            Metadata.IsStale || entry.IsStale);
}
