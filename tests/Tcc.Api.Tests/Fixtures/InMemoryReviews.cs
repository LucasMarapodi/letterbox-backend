using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Social;
namespace Tcc.Infrastructure.Opinions;
public sealed class InMemoryReviews(InMemoryPublicationScope publications) : IReviews
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
    public Task<Review?> AddAsync(Review review, CancellationToken ct = default, Func<Review, CancellationToken, Task>? publish = null) => publications.ExecuteAsync<Review?>(async () =>
    {
        Review saved;
        lock (publications.Gate)
        {
            if (reviews.Values.Any(item => item.UserId == review.UserId && item.GameId == review.GameId)) return null;
            saved = Review.Create(review.Id == 0 ? ++next : review.Id, review.UserId, review.GameId, review.Text, review.CreatedAt);
        }
        if (publish is not null) await publish(saved, ct);
        return TryAdd(saved) ? saved : null;
    }, ct);
    public Task<Review?> FindAsync(int id, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); return Task.FromResult(Find(id)); }
    public Task<IReadOnlyList<Review>> QueryAsync(ReviewQuery query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Review>>(All().Where(item => (query.GameId is null || item.GameId == query.GameId)
            && (query.UserId is null || item.UserId == query.UserId) && (query.Authors is null || query.Authors.Contains(item.UserId))
            && (query.Ids is null || query.Ids.Contains(item.Id))).OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).ToArray());
    }
    public Task<Review?> EditAsync(int id, int actorId, string? text, DateTimeOffset now, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (publications.Gate)
        {
            if (!reviews.TryGetValue(id, out var review)) return Task.FromResult<Review?>(null);
            return Task.FromResult<Review?>(reviews[id] = review.Edit(actorId, text, now));
        }
    }
}