using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Application.Social;

namespace Tcc.Application;

public sealed record DashboardDetails(FavoriteCollections Favorites, IReadOnlyList<ReviewDetails> Feed);
public sealed class PersonalDashboard(PersonalFavorites favorites, FollowingFeed feed, QueryGames games,
    OpinionViews views, ICurrentSession current, SessionAccess access)
{
    public async Task<DashboardDetails> Execute(CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var snapshot = (Favorites: await favorites.SnapshotFor(userId, cancellationToken), Feed: await feed.SnapshotFor(userId, cancellationToken));
        var found = await games.ResolveAsync(snapshot.Favorites.GameIds.Concat(snapshot.Favorites.Reviews.Select(review => review.GameId))
            .Concat(snapshot.Feed.Select(review => review.GameId)), cancellationToken);
        var authors = await views.Authors(snapshot.Favorites.Reviews.Concat(snapshot.Feed).Select(review => review.UserId), cancellationToken);
        var counts = await views.LikeCounts(snapshot.Favorites.Reviews.Concat(snapshot.Feed), cancellationToken);
        return new(favorites.Project(snapshot.Favorites, found, authors, counts), snapshot.Feed
            .Select(review => views.Review(review, found[review.GameId], authors[review.UserId], counts.GetValueOrDefault(review.Id))).ToArray());
    }
}
