using Tcc.Application.Catalog;
using Tcc.Application.Opinions;
using Tcc.Domain.Catalog;

namespace Tcc.Application.Queries;

public sealed record GameActivity(string GameId, int CommentCount, DateTimeOffset LastCommentAt);
public sealed record RankedGameDetails(Game Game, double? AverageRating, int CommentCount, DateTimeOffset LastCommentAt);
public sealed class GamesInDiscussion(CommentActivity activity, QueryGames games, LocalRatingAverage averages)
{
    public IReadOnlyList<RankedGameDetails> Execute(string? period)
    {
        var groups = activity.Execute(period).GroupBy(item => item.Review.GameId)
            .Select(group => new GameActivity(group.Key, group.Sum(item => item.CommentCount), group.Max(item => item.LastCommentAt)));
        return ActivityOrdering.Sort(groups, item => item.CommentCount, item => item.LastCommentAt, item => item.GameId)
            .Select(item => new RankedGameDetails(games.Get(item.GameId), averages.ForGame(item.GameId), item.CommentCount, item.LastCommentAt)).ToArray();
    }
}
