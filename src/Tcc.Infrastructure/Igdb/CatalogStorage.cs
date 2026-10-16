using Tcc.Application.Catalog;
using System.Diagnostics.CodeAnalysis;

namespace Tcc.Infrastructure.Igdb;

internal readonly record struct CatalogKey(string Term, string Sort, int Page, int PageSize, int? GenreId, int? PlatformId)
{
    public static CatalogKey From(CatalogSearch query) => new(query.Term.ToUpperInvariant(), query.Sort, query.Page, query.PageSize, query.GenreId, query.PlatformId);
}
internal static class CatalogStorage
{
    public static CatalogGame Freeze(CatalogGame entry)
    {
        CatalogIds.Parse(entry.Game.Id);
        if (string.IsNullOrWhiteSpace(entry.Game.Title)) throw new CatalogInvalidResponseException();
        return entry with { Game = entry.Game with
        {
            Genres = Array.AsReadOnly(entry.Game.Genres.ToArray()), Platforms = Array.AsReadOnly(entry.Game.Platforms.ToArray()),
            GenreEntries = Array.AsReadOnly(entry.Game.GenreEntries.ToArray()), PlatformEntries = Array.AsReadOnly(entry.Game.PlatformEntries.ToArray())
        }};
    }
    public static CatalogGame Read(CatalogGame entry, DateTimeOffset now, CatalogMemoryOptions options) => entry with
    { Metadata = entry.Metadata with { IsStale = entry.Metadata.IsStale || now >= entry.Metadata.FetchedAt + options.DetailFreshFor } };
    public static bool PageExpired(CatalogMetadata metadata, DateTimeOffset now, CatalogMemoryOptions options) =>
        now > metadata.FetchedAt + options.QueryFreshFor + options.QueryFallbackFor;
    public static void ValidatePage(CatalogPage page)
    {
        if (page.Items.Count > page.Query.PageSize || page.Items.Select(item => item.Game.Id).Distinct().Count() != page.Items.Count)
            throw new CatalogInvalidResponseException();
    }
    public static CatalogPage ReadPage(CatalogPage page, CatalogSearch query, IReadOnlyList<CatalogGame> items,
        DateTimeOffset now, CatalogMemoryOptions options)
    {
        var fetchedAt = items.Select(item => item.Metadata.FetchedAt).Append(page.Metadata.FetchedAt).Min();
        var stale = page.Metadata.IsStale || now >= page.Metadata.FetchedAt + options.QueryFreshFor || items.Any(item => item.Metadata.IsStale);
        return page with { Query = query, Items = Array.AsReadOnly(items.ToArray()), Metadata = new(fetchedAt, stale) };
    }
}
// Owners synchronize this bounded LRU map; indexes never outlive their entries.
internal sealed class BoundedCatalogMap<TKey, TValue>(int capacity) where TKey : notnull where TValue : notnull
{
    private readonly Dictionary<TKey, (TValue Value, LinkedListNode<TKey> Node)> entries = [];
    private readonly LinkedList<TKey> order = new();
    public void Clear() { entries.Clear(); order.Clear(); }
    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (entries.TryGetValue(key, out var entry))
        { order.Remove(entry.Node); order.AddLast(entry.Node); value = entry.Value; return true; }
        value = default;
        return false;
    }
    public void Set(TKey key, TValue value)
    {
        Remove(key);
        entries.Add(key, (value, order.AddLast(key)));
        while (entries.Count > capacity && order.First is not null) Remove(order.First.Value);
    }
    public void Remove(TKey key)
    { if (entries.Remove(key, out var entry)) order.Remove(entry.Node); }
}
