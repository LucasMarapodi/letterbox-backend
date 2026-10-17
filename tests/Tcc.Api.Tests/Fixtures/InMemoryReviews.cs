using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Application;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Social;
namespace Tcc.Infrastructure.Opinions;

public sealed class InMemoryReviews(InMemoryPublicationScope publications, IRatings ratings) : IReviews
{
    private readonly Dictionary<int, Review> reviews = [];
    private int next = 2000;
    public bool TryAdd(Review review)
    {
        lock (publications.Gate)
        {
            if (reviews.ContainsKey(review.Id) || reviews.Values.Any(item => item.UserId == review.UserId && item.GameId == review.GameId)) return false;
            publications.OnRollback(() => reviews.Remove(review.Id)); reviews.Add(review.Id, review); return true;
        }
    }
    public Review? Find(int id) { lock (publications.Gate) return reviews.GetValueOrDefault(id); }
    public IReadOnlyList<Review> All() { lock (publications.Gate) return reviews.Values.ToArray(); }
    public Task<Review> WriteAsync(ReviewWrite command, Func<Review, CancellationToken, Task> publish, CancellationToken ct = default) => publications.ExecuteAsync(async () =>
    {
        var original = All().SingleOrDefault(item => item.UserId == command.UserId && item.GameId == command.GameId);
        if (command.ReviewId is int id && original?.Id != id) throw new ResourceFailure(ResourceFailureKind.NotFound);
        if (original is not null && command.CreateOnly) throw new ResourceFailure(ResourceFailureKind.Conflict);
        var saved = original is null
            ? Review.Create(++next, command.UserId, command.GameId, command.Text, command.Now, command.Title) with { Evaluation = Review.CreateRated(command.UserId, command.GameId, command.Text, command.Rating, command.Now, command.Title).Evaluation }
            : original.EditRated(command.UserId, command.TextSpecified, command.Text, command.Rating, command.Now, command.TitleSpecified, command.Title);
        if (original is null) await publish(saved, ct);
        saved = saved with { Evaluation = await ratings.SetAsync(command.UserId, command.GameId, saved.Evaluation!.Value, command.Now, ct) };
        lock (publications.Gate) reviews[saved.Id] = saved;
        return saved;
    }, ct);
    public Task<Review?> FindAsync(int id, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); return Task.FromResult(Find(id)); }
    public Task<IReadOnlyList<Review>> QueryAsync(ReviewQuery query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Review>>(ReviewOrdering.Common(All().Where(item => (query.GameId is null || item.GameId == query.GameId)
            && (query.UserId is null || item.UserId == query.UserId) && (query.Authors is null || query.Authors.Contains(item.UserId))
            && (query.Ids is null || query.Ids.Contains(item.Id)))).ToArray());
    }
}
