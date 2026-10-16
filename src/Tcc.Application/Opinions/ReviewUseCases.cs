using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Opinions;
using Tcc.Application.Social;

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
    public ReviewDetails Review(Review review)
    {
        var game = games.Get(review.GameId);
        return new(review, Author(review.UserId), new PublicGame(game.Id, game.Title, game.CoverUrl));
    }
}

public sealed class PublishReview(IReviews reviews, QueryGames games, ICurrentSession current,
    SessionAccess access, TimeProvider clock, OpinionViews views, PublicationNotifications notifications, IPublicationScope publications)
{
    public ReviewDetails Execute(string gameId, string? text) => publications.Execute(() =>
    {
        var userId = access.Require(current).UserId;
        games.Get(gameId);
        var review = Review.Create(Guid.NewGuid().ToString("N"), userId, gameId, text, clock.GetUtcNow());
        var result = views.Review(review);
        var events = notifications.ForReview(review);
        if (!reviews.TryAdd(review)) throw new ResourceFailure(ResourceFailureKind.Conflict);
        notifications.Send(events);
        return result;
    });
}

public sealed class GetReview(IReviews reviews, OpinionViews views)
{
    public ReviewDetails Execute(string id) => views.Review(reviews.Find(id) ?? throw new ResourceFailure(ResourceFailureKind.NotFound));
}

public sealed class EditReview(IReviews reviews, ICurrentSession current, SessionAccess access, TimeProvider clock, OpinionViews views)
{
    public ReviewDetails Execute(string id, string? text) => views.Review(
        reviews.Edit(id, access.Require(current).UserId, text, clock.GetUtcNow())
        ?? throw new ResourceFailure(ResourceFailureKind.NotFound));
}
