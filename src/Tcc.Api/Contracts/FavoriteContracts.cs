using Tcc.Domain.Social;
using Tcc.Application.Social;

namespace Tcc.Api.Contracts;

public sealed record FavoriteResponse(int Id, int UserId, string TargetType, int TargetId)
{
    public static FavoriteResponse From(Favorite item) =>
        new(item.Id, item.UserId, item.Kind == FavoriteKind.Game ? "game" : "review", item.TargetId);
}
public sealed record FavoriteStatusResponse(FavoriteResponse? Favorite);
public sealed record FavoriteListsResponse(IReadOnlyList<GameResponse> Games, IReadOnlyList<ReviewResponse> Reviews)
{
    public static FavoriteListsResponse From(FavoriteCollections lists, Func<int, double?> average) =>
        new(lists.Games.Select(game => GameResponse.From(game, average(game.Id))).ToArray(),
            lists.Reviews.Select(ReviewResponse.From).ToArray());
}
