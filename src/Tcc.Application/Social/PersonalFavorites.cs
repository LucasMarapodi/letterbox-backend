using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Domain.Catalog;
using Tcc.Domain.Social;

namespace Tcc.Application.Social;

public interface IFavorites
{
    Task<Favorite> SetAsync(int userId, FavoriteKind kind, int targetId, DateTimeOffset now, CancellationToken ct = default);
    Task<Favorite?> FindAsync(int userId, FavoriteKind kind, int targetId, CancellationToken ct = default);
    Task RemoveAsync(int userId, FavoriteKind kind, int targetId, CancellationToken ct = default);
    Task<FavoriteSnapshot> SnapshotAsync(int userId, CancellationToken ct = default);
}
public sealed record FavoriteCollections(IReadOnlyList<Game> Games, IReadOnlyList<ReviewDetails> Reviews);
public sealed record FavoriteSnapshot(IReadOnlyList<int> GameIds, IReadOnlyList<Tcc.Domain.Opinions.Review> Reviews);

public sealed class PersonalFavorites(IFavorites favorites, QueryGames games, GetReview reviews,
    ICurrentSession current, SessionAccess access, OpinionViews views, TimeProvider clock)
{
    private async Task<FavoriteKind> Validate(string kind, int targetId, CancellationToken cancellationToken)
    {
        if (targetId <= 0) throw new Tcc.Domain.Identity.DomainValidationException("ID local inválido.");
        var type = Favorite.ParseKind(kind);
        if (type == FavoriteKind.Game) await games.GetAsync(targetId, cancellationToken); else await reviews.FindAsync(targetId, cancellationToken);
        return type;
    }
    public async Task<Favorite> Set(string kind, int targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var type = await Validate(kind, targetId, cancellationToken);
        await access.Require(current, cancellationToken);
        return await favorites.SetAsync(userId, type, targetId, clock.GetUtcNow(), cancellationToken);
    }
    public async Task<Favorite?> Get(string kind, int targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var type = await Validate(kind, targetId, cancellationToken);
        return await favorites.FindAsync(userId, type, targetId, cancellationToken);
    }
    public async Task Remove(string kind, int targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var type = await Validate(kind, targetId, cancellationToken);
        await access.Require(current, cancellationToken);
        await favorites.RemoveAsync(userId, type, targetId, cancellationToken);
    }
    public async Task<FavoriteSnapshot> Snapshot(CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        return await SnapshotFor(userId, cancellationToken);
    }
    public Task<FavoriteSnapshot> SnapshotFor(int userId, CancellationToken ct = default) => favorites.SnapshotAsync(userId, ct);
    public FavoriteCollections Project(FavoriteSnapshot snapshot, IReadOnlyDictionary<int, Game> found, IReadOnlyDictionary<int, PublicAuthor> authors, IReadOnlyDictionary<int, int> counts) =>
        new(snapshot.GameIds.Select(id => found[id]).ToArray(), ReviewOrdering.Common(snapshot.Reviews).Select(review => views.Review(review, found[review.GameId], authors[review.UserId], counts.GetValueOrDefault(review.Id))).ToArray());
    public async Task<FavoriteCollections> List(CancellationToken cancellationToken = default)
    {
        var snapshot = await Snapshot(cancellationToken);
        var found = await games.ResolveAsync(snapshot.GameIds.Concat(snapshot.Reviews.Select(review => review.GameId)), cancellationToken);
        var authors = await views.Authors(snapshot.Reviews.Select(review => review.UserId), cancellationToken);
        return Project(snapshot, found, authors, await views.LikeCounts(snapshot.Reviews, cancellationToken));
    }
}
