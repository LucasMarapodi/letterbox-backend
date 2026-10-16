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
public sealed record FavoriteSnapshot(IReadOnlyList<int> GameIds, IReadOnlyList<Tcc.Domain.Opinions.Review> Reviews);

public sealed class PersonalFavorites(IFavorites favorites, QueryGames games, GetReview reviews,
    ICurrentSession current, SessionAccess access, OpinionViews views, IPublicationScope publications, IReviews reviewStore)
{
    private static int LocalTarget(string value) => int.TryParse(value, System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw new Tcc.Domain.Identity.DomainValidationException("ID local inválido.");
    private async Task<FavoriteKind> Validate(string kind, string targetId, CancellationToken cancellationToken)
    {
        var type = Favorite.ParseKind(kind);
        if (type == FavoriteKind.Game) await games.GetAsync(LocalTarget(targetId), cancellationToken); else await reviews.FindAsync(LocalTarget(targetId), cancellationToken);
        return type;
    }
    public async Task<Favorite> Set(string kind, string targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var type = await Validate(kind, targetId, cancellationToken);
        targetId = LocalTarget(targetId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await access.Require(current, cancellationToken);
        return publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return favorites.Set(userId, type, targetId);
        });
    }
    public async Task<Favorite?> Get(string kind, string targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var type = await Validate(kind, targetId, cancellationToken);
        return favorites.Find(userId, type, LocalTarget(targetId).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    public async Task Remove(string kind, string targetId, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var type = await Validate(kind, targetId, cancellationToken);
        targetId = LocalTarget(targetId).ToString(System.Globalization.CultureInfo.InvariantCulture);
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
        return await SnapshotFor(userId, cancellationToken);
    }
    public async Task<FavoriteSnapshot> SnapshotFor(int userId, CancellationToken ct = default)
    {
        var items = publications.Execute(() => favorites.ForUser(userId).OrderBy(item => item.TargetId, StringComparer.Ordinal).ToArray());
        var ids = items.Where(item => item.Kind == FavoriteKind.Review).Select(item => LocalTarget(item.TargetId)).ToArray();
        var found = new Dictionary<int, Tcc.Domain.Opinions.Review>();
        foreach (var chunk in ids.Distinct().Chunk(500))
            foreach (var review in await reviewStore.QueryAsync(new(Ids: chunk), ct)) found.Add(review.Id, review);
        return new(items.Where(item => item.Kind == FavoriteKind.Game).Select(item => LocalTarget(item.TargetId)).ToArray(),
            ids.Select(id => found.GetValueOrDefault(id) ?? throw new ResourceFailure(ResourceFailureKind.NotFound)).ToArray());
    }
    public FavoriteCollections Project(FavoriteSnapshot snapshot, IReadOnlyDictionary<int, Game> found, IReadOnlyDictionary<int, PublicAuthor> authors) =>
        new(snapshot.GameIds.Select(id => found[id]).ToArray(), snapshot.Reviews.Select(review => views.Review(review, found[review.GameId], authors[review.UserId])).ToArray());
    public async Task<FavoriteCollections> List(CancellationToken cancellationToken = default)
    {
        var snapshot = await Snapshot(cancellationToken);
        var found = await games.ResolveAsync(snapshot.GameIds.Concat(snapshot.Reviews.Select(review => review.GameId)), cancellationToken);
        var authors = await views.Authors(snapshot.Reviews.Select(review => review.UserId), cancellationToken);
        return Project(snapshot, found, authors);
    }
}
