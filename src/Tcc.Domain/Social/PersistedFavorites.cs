namespace Tcc.Domain.Social;

public sealed record GameFavorite(int Id, int UserId, int GameId, DateTimeOffset CreatedAt);
public sealed record ReviewFavorite(int Id, int UserId, int ReviewId, DateTimeOffset CreatedAt);
