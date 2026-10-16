using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Domain.Catalog;
using Tcc.Domain.Social;

namespace Tcc.Application.Social;

public interface IFavorites
{
    Favorite Set(string userId, FavoriteKind kind, string targetId);
    Favorite? Find(string userId, FavoriteKind kind, string targetId);
    void Remove(string userId, FavoriteKind kind, string targetId);
    IReadOnlyList<Favorite> ForUser(string userId);
}
public sealed record FavoriteCollections(IReadOnlyList<Game> Games, IReadOnlyList<ReviewDetails> Reviews);
public sealed record FavoriteSnapshot(IReadOnlyList<string> GameIds, IReadOnlyList<Tcc.Domain.Opinions.Review> Reviews);

public sealed class PersonalFavorites(IFavorites favorites, QueryGames games, GetReview reviews,
    ICurrentSession current, SessionAccess access, OpinionViews views, IPublicationScope publications)
{
    private async Task<FavoriteKind> Validate(string kind, string targetId, CancellationToken cancellationToken)
    {
        var type = Favorite.ParseKind(kind);
        if (type == FavoriteKind.Game) await games.GetAsync(targetId, cancellationToken); else reviews.Find(targetId);
        return type;
    }
    public async Task<Favorite> Set(string kind, string targetId, CancellationToken cancellationToken = default)
    {
        var userId = access.Require(current).UserId;
        var type = await Validate(kind, targetId, cancellationToken);
        return publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            access.Require(current);
            if (type == FavoriteKind.Review) reviews.Find(targetId);
            return favorites.Set(userId, type, targetId);
        });
    }
    public async Task<Favorite?> Get(string kind, string targetId, CancellationToken cancellationToken = default)
    {
        var userId = access.Require(current).UserId;
        return favorites.Find(userId, await Validate(kind, targetId, cancellationToken), targetId);
    }
    public async Task Remove(string kind, string targetId, CancellationToken cancellationToken = default)
    {
        var userId = access.Require(current).UserId;
        var type = await Validate(kind, targetId, cancellationToken);
        publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            access.Require(current);
            favorites.Remove(userId, type, targetId);
            return true;
        });
    }
    public FavoriteSnapshot Snapshot() => publications.Execute(() =>
    {
        var items = favorites.ForUser(access.Require(current).UserId).OrderBy(item => item.TargetId, StringComparer.Ordinal).ToArray();
        return new FavoriteSnapshot(items.Where(item => item.Kind == FavoriteKind.Game).Select(item => item.TargetId).ToArray(),
            items.Where(item => item.Kind == FavoriteKind.Review).Select(item => reviews.Find(item.TargetId)).ToArray());
    });
    public FavoriteCollections Project(FavoriteSnapshot snapshot, IReadOnlyDictionary<string, Game> found) =>
        new(snapshot.GameIds.Select(id => found[id]).ToArray(), snapshot.Reviews.Select(review => views.Review(review, found[review.GameId])).ToArray());
    public async Task<FavoriteCollections> List(CancellationToken cancellationToken = default)
    {
        var snapshot = Snapshot();
        var found = await games.ResolveAsync(snapshot.GameIds.Concat(snapshot.Reviews.Select(review => review.GameId)), cancellationToken);
        return Project(snapshot, found);
    }
}
