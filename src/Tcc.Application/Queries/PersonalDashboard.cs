using Tcc.Application.Opinions;
using Tcc.Application.Social;

namespace Tcc.Application.Queries;

public sealed record DashboardDetails(FavoriteCollections Favorites, IReadOnlyList<ReviewDetails> Feed);
public sealed class PersonalDashboard(PersonalFavorites favorites, FollowingFeed feed, Tcc.Application.Catalog.QueryGames games,
    OpinionViews views, IPublicationScope publications)
{
    public async Task<DashboardDetails> Execute(CancellationToken cancellationToken = default)
    {
        var snapshot = publications.Execute(() => (Favorites: favorites.Snapshot(), Feed: feed.Snapshot()));
        var found = await games.ResolveAsync(snapshot.Favorites.GameIds.Concat(snapshot.Favorites.Reviews.Select(review => review.GameId))
            .Concat(snapshot.Feed.Select(review => review.GameId)), cancellationToken);
        return new(favorites.Project(snapshot.Favorites, found), snapshot.Feed.OrderByDescending(review => review.CreatedAt)
            .ThenBy(review => review.Id, StringComparer.Ordinal).Select(review => views.Review(review, found[review.GameId])).ToArray());
    }
}
