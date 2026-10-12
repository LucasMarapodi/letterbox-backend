using Tcc.Domain.Catalog;

namespace Tcc.Api.Contracts;

public sealed record GameResponse(string Id, string Title, string Summary, string? CoverUrl,
    IReadOnlyList<string> Genres, IReadOnlyList<string> Platforms, DateOnly? ReleaseDate, double? AverageRating)
{
    public static GameResponse From(Game game, double? average = null) =>
        new(game.Id, game.Title, game.Summary, game.CoverUrl, game.Genres, game.Platforms, game.ReleaseDate, average);
}
