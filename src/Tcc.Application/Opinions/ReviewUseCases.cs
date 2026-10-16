using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Catalog;

namespace Tcc.Application.Opinions;
public interface IReviews
{
    bool TryAdd(Review review);
    Review? Find(string id);
    IReadOnlyList<Review> All();
    Review? Edit(string id, string actorId, string? text, DateTimeOffset now);
}
public sealed record PublicAuthor(string Id, string DisplayName, string? AvatarUrl);
public sealed record PublicGame(string Id, string Title, string? CoverUrl);
public sealed record ReviewDetails(Review Review, PublicAuthor Author, PublicGame Game);
public sealed class OpinionViews(IUserAccounts accounts, QueryGames games)
{
    public PublicAuthor Author(string userId)
    {
        var account = accounts.FindById(userId) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
        return new(account.Id, account.Profile.DisplayName, account.Profile.AvatarUrl);
    }
    public ReviewDetails Review(Review review, Game game) => new(review, Author(review.UserId), new(game.Id, game.Title, game.CoverUrl));
    public async Task<ReviewDetails> ReviewAsync(Review review, CancellationToken cancellationToken = default) =>
        Review(review, await games.GetAsync(review.GameId, cancellationToken));
}
public sealed class PublishReview(IReviews reviews, QueryGames games, ICurrentSession current,
    SessionAccess access, TimeProvider clock, OpinionViews views, PublicationNotifications notifications, IPublicationScope publications)
{
    public async Task<ReviewDetails> Execute(string gameId, string? text, CancellationToken cancellationToken = default)
    {
        var userId = access.Require(current).UserId;
        var draft = Review.Create(Guid.NewGuid().ToString("N"), userId, gameId, text, clock.GetUtcNow());
        var game = await games.GetAsync(gameId, cancellationToken);
        var author = views.Author(userId);
        cancellationToken.ThrowIfCancellationRequested();
        return publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            access.Require(current);
            var review = Review.Create(draft.Id, userId, gameId, draft.Text, clock.GetUtcNow());
            var result = new ReviewDetails(review, author, new(game.Id, game.Title, game.CoverUrl));
            var events = notifications.ForReview(review);
            if (!reviews.TryAdd(review)) throw new ResourceFailure(ResourceFailureKind.Conflict);
            notifications.Send(events);
            return result;
        });
    }
}
public sealed class GetReview(IReviews reviews, OpinionViews views)
{
    public Review Find(string id) => reviews.Find(id) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
    public Task<ReviewDetails> ExecuteAsync(string id, CancellationToken cancellationToken = default) => views.ReviewAsync(Find(id), cancellationToken);
}
public sealed class EditReview(IReviews reviews, ICurrentSession current, SessionAccess access, TimeProvider clock,
    OpinionViews views, IPublicationScope publications)
{
    public async Task<ReviewDetails> Execute(string id, string? text, CancellationToken cancellationToken = default)
    {
        var userId = access.Require(current).UserId;
        var original = reviews.Find(id) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
        var prepared = original.Edit(userId, text, clock.GetUtcNow());
        var projection = await views.ReviewAsync(prepared, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            access.Require(current);
            var updated = reviews.Edit(id, userId, prepared.Text, clock.GetUtcNow()) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
            return projection with { Review = updated };
        });
    }
}
