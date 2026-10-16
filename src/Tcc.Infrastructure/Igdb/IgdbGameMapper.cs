using Tcc.Application.Catalog;
using Tcc.Domain.Catalog;

namespace Tcc.Infrastructure.Igdb;

public static class IgdbGameMapper
{
    public static CatalogGame Map(IgdbGameDto dto, DateTimeOffset fetchedAt)
    {
        if (dto.Id is null or <= 0 || string.IsNullOrWhiteSpace(dto.Name)) throw new CatalogInvalidResponseException();
        DateOnly? release = null;
        if (dto.FirstReleaseDate is long seconds)
        {
            try { release = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime); }
            catch (ArgumentOutOfRangeException) { throw new CatalogInvalidResponseException(); }
        }
        string? cover = null;
        if (!string.IsNullOrWhiteSpace(dto.Cover?.ImageId))
        {
            var imageId = dto.Cover.ImageId;
            if (imageId.Length > 128 || imageId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
                throw new CatalogInvalidResponseException();
            cover = "https://images.igdb.com/igdb/image/upload/t_cover_big/" + imageId + ".jpg";
        }
        var genres = Names(dto.Genres);
        var platforms = Names(dto.Platforms);
        var game = new Game(0, dto.Name.Trim(), dto.Summary ?? "", cover,
            genres.Select(item => item.Name).ToArray(), platforms.Select(item => item.Name).ToArray(), release)
        {
            IgdbId = dto.Id.Value, FetchedAt = fetchedAt.ToUniversalTime(),
            GenreEntries = genres.Select(item => new Genre(0, item.Id, item.Name)).ToArray(),
            PlatformEntries = platforms.Select(item => new Platform(0, item.Id, item.Name)).ToArray()
        };
        return new(game, new CatalogMetadata(fetchedAt.ToUniversalTime()));
    }
    private static IReadOnlyList<(long Id, string Name)> Names(IgdbNameDto[]? items)
    {
        if (items is null) return [];
        if (items.Any(item => item is null || item.Id is null or <= 0 || string.IsNullOrWhiteSpace(item.Name))) throw new CatalogInvalidResponseException();
        if (items.GroupBy(item => item.Id).Any(group => group.Select(item => item.Name!.Trim()).Distinct().Count() > 1)) throw new CatalogInvalidResponseException();
        return items.DistinctBy(item => item.Id).Select(item => (item.Id!.Value, item.Name!.Trim())).ToArray();
    }
}
