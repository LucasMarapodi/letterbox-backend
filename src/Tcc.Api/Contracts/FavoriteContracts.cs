using Tcc.Domain.Social;

namespace Tcc.Api.Contracts;

public sealed record FavoriteResponse(string Id, string UserId, string TargetType, string TargetId)
{
    public static FavoriteResponse From(Favorite item) =>
        new(item.Id, item.UserId, item.Kind == FavoriteKind.Game ? "game" : "review", item.TargetId);
}
public sealed record FavoriteStatusResponse(FavoriteResponse? Favorite);
public sealed record FavoriteListsResponse(IReadOnlyList<GameResponse> Games, IReadOnlyList<ReviewResponse> Reviews);
