using Tcc.Application.Opinions;
using Tcc.Application.Social;

namespace Tcc.Application.Queries;

public sealed record DashboardDetails(FavoriteCollections Favorites, IReadOnlyList<ReviewDetails> Feed);
public sealed class PersonalDashboard(PersonalFavorites favorites, FollowingFeed feed, Tcc.Application.Catalog.QueryGames games,
    OpinionViews views, IPublicationScope publications, Tcc.Application.Identity.ICurrentSession current, Tcc.Application.Identity.SessionAccess access)
{
    public async Task<DashboardDetails> Execute(CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var snapshot = publications.Execute(() => (Favorites: favorites.SnapshotFor(userId), Feed: feed.SnapshotFor(userId)));
        var found = await games.ResolveAsync(snapshot.Favorites.GameIds.Concat(snapshot.Favorites.Reviews.Select(review => review.GameId))
            .Concat(snapshot.Feed.Select(review => review.GameId)), cancellationToken);
        var authors = await views.Authors(snapshot.Favorites.Reviews.Concat(snapshot.Feed).Select(review => review.UserId), cancellationToken);
        return new(favorites.Project(snapshot.Favorites, found, authors), snapshot.Feed.OrderByDescending(review => review.CreatedAt)
            .ThenBy(review => review.Id, StringComparer.Ordinal).Select(review => views.Review(review, found[review.GameId], authors[review.UserId])).ToArray());
    }
}
