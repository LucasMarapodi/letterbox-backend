namespace Tcc.Api.Contracts;

public sealed record DashboardResponse(FavoriteListsResponse Favorites, IReadOnlyList<ReviewResponse> Feed);
