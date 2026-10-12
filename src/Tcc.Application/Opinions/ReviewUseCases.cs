using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Opinions;

public interface IReviews
{
    bool TryAdd(Review review);
    Review? Find(string id);
    IReadOnlyList<Review> All();
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
    SessionAccess access, TimeProvider clock, OpinionViews views)
{
    public ReviewDetails Execute(string gameId, string? text)
    {
        var userId = access.Require(current).UserId;
        games.Get(gameId);
        var review = Review.Create(Guid.NewGuid().ToString("N"), userId, gameId, text, clock.GetUtcNow());
        if (!reviews.TryAdd(review)) throw new ResourceFailure(ResourceFailureKind.Conflict);
        return views.Review(review);
    }
}

public sealed class GetReview(IReviews reviews, OpinionViews views)
{
    public ReviewDetails Execute(string id) => views.Review(reviews.Find(id) ?? throw new ResourceFailure(ResourceFailureKind.NotFound));
}
