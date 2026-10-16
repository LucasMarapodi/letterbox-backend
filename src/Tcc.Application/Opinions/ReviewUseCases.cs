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
    Review? Edit(string id, int actorId, string? text, DateTimeOffset now);
}
public sealed record PublicAuthor(int Id, string DisplayName, string? AvatarUrl);
public sealed record PublicGame(string Id, string Title, string? CoverUrl);
public sealed record ReviewDetails(Review Review, PublicAuthor Author, PublicGame Game);
public sealed class OpinionViews(IUserAccounts accounts, QueryGames games)
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
    public ReviewDetails Review(Review review, Game game, PublicAuthor author) => new(review, author, new(game.Id, game.Title, game.CoverUrl));
    public async Task<ReviewDetails> ReviewAsync(Review review, CancellationToken cancellationToken = default) =>
        Review(review, await games.GetAsync(review.GameId, cancellationToken), await Author(review.UserId, cancellationToken));
}
public sealed class PublishReview(IReviews reviews, QueryGames games, ICurrentSession current,
    SessionAccess access, TimeProvider clock, OpinionViews views, PublicationNotifications notifications, IPublicationScope publications)
{
    public async Task<ReviewDetails> Execute(string gameId, string? text, CancellationToken cancellationToken = default)
    {
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var draft = Review.Create(Guid.NewGuid().ToString("N"), userId, gameId, text, clock.GetUtcNow());
        var game = await games.GetAsync(gameId, cancellationToken);
        var author = await views.Author(userId, cancellationToken);
        await access.Require(current, cancellationToken);
        return publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        var userId = (await access.Require(current, cancellationToken)).UserId;
        var original = reviews.Find(id) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
        var prepared = original.Edit(userId, text, clock.GetUtcNow());
        var projection = await views.ReviewAsync(prepared, cancellationToken);
        await access.Require(current, cancellationToken);
        return publications.Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var updated = reviews.Edit(id, userId, prepared.Text, clock.GetUtcNow()) ?? throw new ResourceFailure(ResourceFailureKind.NotFound);
            return projection with { Review = updated };
        });
    }
}
