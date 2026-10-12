using Tcc.Application.Opinions;
using Tcc.Application.Social;

namespace Tcc.Application.Queries;

public sealed record DashboardDetails(FavoriteCollections Favorites, IReadOnlyList<ReviewDetails> Feed);
public sealed class PersonalDashboard(PersonalFavorites favorites, FollowingFeed feed)
{
    public DashboardDetails Execute() => new(favorites.List(), feed.Execute());
}
