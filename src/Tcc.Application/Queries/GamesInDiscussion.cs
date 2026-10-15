using Tcc.Application.Catalog;
using Tcc.Application.Opinions;
using Tcc.Domain.Catalog;

namespace Tcc.Application.Queries;

public sealed record GameActivity(int GameId, int CommentCount, DateTimeOffset LastCommentAt);
public sealed record RankedGameDetails(Game Game, double? AverageRating, int CommentCount, DateTimeOffset LastCommentAt);
public sealed class GamesInDiscussion(CommentActivity activity, QueryGames games, LocalRatingAverage averages)
{
    public async Task<IReadOnlyList<RankedGameDetails>> Execute(string? period, CancellationToken cancellationToken = default)
    {
        var groups = activity.Execute(period).GroupBy(item => item.Review.GameId)
            .Select(group => new GameActivity(group.Key, group.Sum(item => item.CommentCount), group.Max(item => item.LastCommentAt)));
        var snapshot = ActivityOrdering.Sort(groups, item => item.CommentCount, item => item.LastCommentAt, item => item.GameId)
            .Select(item => (Activity: item, Average: averages.ForGame(item.GameId))).ToArray();
        var found = await games.ResolveAsync(snapshot.Select(item => item.Activity.GameId), cancellationToken);
        return snapshot.Select(item => new RankedGameDetails(found[item.Activity.GameId], item.Average,
            item.Activity.CommentCount, item.Activity.LastCommentAt)).ToArray();
    }
}
