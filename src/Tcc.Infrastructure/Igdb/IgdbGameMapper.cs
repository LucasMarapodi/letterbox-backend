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
        var game = new Game(CatalogIds.Format(dto.Id.Value), dto.Name.Trim(), dto.Summary ?? "", cover,
            Names(dto.Genres), Names(dto.Platforms), release);
        return new(game, new CatalogMetadata(fetchedAt.ToUniversalTime()));
    }
    private static IReadOnlyList<string> Names(IgdbNameDto[]? items)
    {
        if (items is null) return Array.Empty<string>();
        if (items.Any(item => item is null)) throw new CatalogInvalidResponseException();
        return Array.AsReadOnly(items.Select(item => item.Name?.Trim()).OfType<string>()
            .Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).ToArray());
    }
}
