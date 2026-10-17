using Tcc.Application.Catalog;
using Tcc.Domain.Catalog;

namespace Tcc.Application.Opinions;

public sealed record GameActivity(int GameId, int CommentCount, DateTimeOffset LastCommentAt);
public sealed record RankedGameDetails(Game Game, double? AverageRating, int CommentCount, DateTimeOffset LastCommentAt);
public sealed class GamesInDiscussion(CommentActivity activity, QueryGames games, LocalRatingAverage averages)
{
    public async Task<IReadOnlyList<RankedGameDetails>> Execute(string? period, CancellationToken cancellationToken = default)
    {
        var groups = (await activity.Execute(period, cancellationToken)).GroupBy(item => item.Review.GameId)
            .Select(group => new GameActivity(group.Key, group.Sum(item => item.CommentCount), group.Max(item => item.LastCommentAt)));
        var snapshot = ActivityOrdering.Sort(groups, item => item.CommentCount, item => item.LastCommentAt, item => item.GameId)
            .ToArray();
        var found = await games.ResolveAsync(snapshot.Select(item => item.GameId), cancellationToken);
        var values = await averages.ForGames(snapshot.Select(item => item.GameId), cancellationToken);
        return snapshot.Select(item => new RankedGameDetails(found[item.GameId], values.GetValueOrDefault(item.GameId),
            item.CommentCount, item.LastCommentAt)).ToArray();
    }
}
