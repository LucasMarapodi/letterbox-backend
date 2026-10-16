using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Domain.Catalog;
using Tcc.Domain.Social;

namespace Tcc.Application.Social;

public interface IFavorites
{
    Favorite Set(int userId, FavoriteKind kind, string targetId);
    Favorite? Find(int userId, FavoriteKind kind, string targetId);
    void Remove(int userId, FavoriteKind kind, string targetId);
    IReadOnlyList<Favorite> ForUser(int userId);
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
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var type = await Validate(kind, targetId, cancellationToken);
        await access.Require(current, cancellationToken);
        return publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (type == FavoriteKind.Review) reviews.Find(targetId);
            return favorites.Set(userId, type, targetId);
        });
    }
    public async Task<Favorite?> Get(string kind, string targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        return favorites.Find(userId, await Validate(kind, targetId, cancellationToken), targetId);
    }
    public async Task Remove(string kind, string targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var type = await Validate(kind, targetId, cancellationToken);
        await access.Require(current, cancellationToken);
        publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            favorites.Remove(userId, type, targetId);
            return true;
        });
    }
    public async Task<FavoriteSnapshot> Snapshot(CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        return publications.Execute(() => SnapshotFor(userId));
    }
    public FavoriteSnapshot SnapshotFor(int userId)
    {
        var items = favorites.ForUser(userId).OrderBy(item => item.TargetId, StringComparer.Ordinal).ToArray();
        return new(items.Where(item => item.Kind == FavoriteKind.Game).Select(item => item.TargetId).ToArray(),
            items.Where(item => item.Kind == FavoriteKind.Review).Select(item => reviews.Find(item.TargetId)).ToArray());
    }
    public FavoriteCollections Project(FavoriteSnapshot snapshot, IReadOnlyDictionary<string, Game> found, IReadOnlyDictionary<int, PublicAuthor> authors) =>
        new(snapshot.GameIds.Select(id => found[id]).ToArray(), snapshot.Reviews.Select(review => views.Review(review, found[review.GameId], authors[review.UserId])).ToArray());
    public async Task<FavoriteCollections> List(CancellationToken cancellationToken = default)
    {
        var snapshot = await Snapshot(cancellationToken);
        var found = await games.ResolveAsync(snapshot.GameIds.Concat(snapshot.Reviews.Select(review => review.GameId)), cancellationToken);
        var authors = await views.Authors(snapshot.Reviews.Select(review => review.UserId), cancellationToken);
        return Project(snapshot, found, authors);
    }
}
