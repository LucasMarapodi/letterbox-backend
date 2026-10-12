namespace Tcc.Domain.Catalog;

public sealed record Game(string Id, string Title, string Summary, string? CoverUrl,
    IReadOnlyList<string> Genres, IReadOnlyList<string> Platforms, DateOnly? ReleaseDate);
