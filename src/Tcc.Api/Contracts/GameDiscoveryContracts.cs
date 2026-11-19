namespace Tcc.Api.Contracts;

public sealed record DiscoveryPageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasNextPage);
public sealed record TopRatedGameResponse(GameResponse Game, double AverageRating, int RatingCount, DateTimeOffset LastRatedAt);
public sealed record TrendingGameResponse(GameResponse Game, DateTimeOffset LastRatedAt);
public sealed record RecommendedGameResponse(GameResponse Game, int Score, IReadOnlyList<string> MatchedGenres, IReadOnlyList<string> MatchedThemes);
public sealed record RecommendationsPageResponse(IReadOnlyList<RecommendedGameResponse> Items, int Page, int PageSize, bool HasNextPage, string? Reason);
