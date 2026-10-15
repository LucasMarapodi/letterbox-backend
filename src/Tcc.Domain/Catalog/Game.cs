namespace Tcc.Domain.Catalog;

public sealed record Game(int Id, string Title, string Summary, string? CoverUrl,
    IReadOnlyList<string> Genres, IReadOnlyList<string> Platforms, DateOnly? ReleaseDate)
{
    private Game() : this(0, "", "", null, [], [], null) { }
    public long IgdbId { get; init; }
    public DateTimeOffset FetchedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyList<Genre> GenreEntries { get; init; } = [];
    public IReadOnlyList<Platform> PlatformEntries { get; init; } = [];
}

public sealed record Genre(int Id, long IgdbId, string Name);
public sealed record Platform(int Id, long IgdbId, string Name);
public sealed record GameGenre(int GameId, int GenreId);
public sealed record GamePlatform(int GameId, int PlatformId);
