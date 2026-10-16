using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Catalog;

namespace Tcc.Application.Opinions;
public sealed record ReviewQuery(int? GameId = null, int? UserId = null, IReadOnlyList<int>? Authors = null, IReadOnlyList<int>? Ids = null);
public sealed record ReviewWrite(int UserId, int GameId, int? ReviewId, bool CreateOnly, bool TextSpecified, string? Text, double? Rating, DateTimeOffset Now, bool TitleSpecified = false, string? Title = null);
public interface IReviews
{
    Task<Review> WriteAsync(ReviewWrite command, Func<Review, CancellationToken, Task> publish, CancellationToken ct = default);
    Task<Review?> FindAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Review>> QueryAsync(ReviewQuery query, CancellationToken ct = default);
}
public sealed record PublicAuthor(int Id, string DisplayName, string? AvatarUrl);
public sealed record PublicGame(int Id, string Title, string? CoverUrl);
public sealed record ReviewDetails(Review Review, PublicAuthor Author, PublicGame Game, int LikeCount = 0);
public sealed class OpinionViews(IUserAccounts accounts, QueryGames games, IReviewLikes likes)
{
    public async Task<PublicAuthor> Author(int userId, CancellationToken cancellationToken = default)
    {
        var account = await accounts.FindByIdAsync(userId, cancellationToken) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
        return new(account.Id, account.Profile.DisplayName, account.Profile.AvatarUrl);
    }
    public async Task<IReadOnlyDictionary<int, PublicAuthor>> Authors(IEnumerable<int> userIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, PublicAuthor>();
        foreach (var chunk in userIds.Distinct().Chunk(500))
        {
            var found = await accounts.FindManyAsync(chunk, cancellationToken);
            foreach (var id in chunk)
            {
                if (!found.TryGetValue(id, out var account)) throw new ResourceFailure(ResourceFailureKind.NotFound);
                result.Add(id, new(id, account.Profile.DisplayName, account.Profile.AvatarUrl));
            }
        }
        return result;
    }
    public async Task<IReadOnlyDictionary<int, int>> LikeCounts(IEnumerable<Review> reviews, CancellationToken ct = default)
    {
        var result = new Dictionary<int,int>();
        foreach (var chunk in reviews.Select(review => review.Id).Distinct().Chunk(500))
            foreach (var entry in await likes.CountsAsync(chunk, ct)) result.Add(entry.Key, entry.Value);
        return result;
    }
    public ReviewDetails Review(Review review, Game game, PublicAuthor author, int likeCount) => new(review, author, new(game.Id, game.Title, game.CoverUrl), likeCount);
    public async Task<ReviewDetails> ReviewAsync(Review review, CancellationToken cancellationToken = default) =>
        Review(review, await games.GetAsync(review.GameId, cancellationToken), await Author(review.UserId, cancellationToken),
            (await LikeCounts([review], cancellationToken)).GetValueOrDefault(review.Id));
}
public sealed class PublishReview(IReviews reviews, QueryGames games, ICurrentSession current,
    SessionAccess access, TimeProvider clock, OpinionViews views, PublicationNotifications notifications)
{
    public async Task<ReviewDetails> Execute(int gameId, string? text, double? rating, CancellationToken ct = default, bool createOnly = true, string? title = null)
    {
        var userId = (await access.Require(current, ct)).UserId;
        var draft = Review.CreateRated(userId, gameId, text, rating, clock.GetUtcNow(), title);
        var game = await games.GetAsync(gameId, ct);
        var author = await views.Author(userId, ct);
        await access.Require(current, ct);
        ct.ThrowIfCancellationRequested();
        var review = await reviews.WriteAsync(new(userId, gameId, null, createOnly, createOnly, draft.Text, rating, clock.GetUtcNow(), createOnly, draft.Title), notifications.ForReviewAsync, ct);
        return new(review, author, new(game.Id, game.Title, game.CoverUrl));
    }
}
public sealed class GetReview(IReviews reviews, OpinionViews views)
{
    public async Task<Review> FindAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) throw new Tcc.Domain.Identity.DomainValidationException("ID de review inválido.");
        return await reviews.FindAsync(id, ct) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
    }
    public async Task<ReviewDetails> ExecuteAsync(int id, CancellationToken ct = default) => await views.ReviewAsync(await FindAsync(id, ct), ct);
}
public sealed class EditReview(IReviews reviews, ICurrentSession current, SessionAccess access, TimeProvider clock, OpinionViews views)
{
    public async Task<ReviewDetails> Execute(int id, bool textSpecified, string? text, double? rating, CancellationToken ct = default, bool titleSpecified = false, string? title = null)
    {
        var userId = (await access.Require(current, ct)).UserId;
        if (id <= 0) throw new Tcc.Domain.Identity.DomainValidationException("ID de review inválido.");
        var original = await reviews.FindAsync(id, ct) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
        var prepared = original.EditRated(userId, textSpecified, text, rating, clock.GetUtcNow(), titleSpecified, title);
        var projection = await views.ReviewAsync(prepared, ct);
        await access.Require(current, ct);
        var updated = await reviews.WriteAsync(new(userId, original.GameId, id, false, textSpecified, text, rating, clock.GetUtcNow(), titleSpecified, title), (_, _) => Task.CompletedTask, ct);
        return projection with { Review = updated };
    }
}
