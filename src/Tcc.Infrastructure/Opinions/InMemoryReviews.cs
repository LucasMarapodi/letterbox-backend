using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;

namespace Tcc.Infrastructure.Opinions;

public sealed class InMemoryReviews : IReviews
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, Review> reviews = new(StringComparer.Ordinal);
    private readonly HashSet<(string User, string Game)> pairs = [];

    public InMemoryReviews() => TryAdd(Review.Create("review-fixture", "user-1", "game-3", "Review fictícia de Atlas.",
        new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)));

    public bool TryAdd(Review review)
    {
        lock (gate)
        {
            if (reviews.ContainsKey(review.Id) || pairs.Contains((review.UserId, review.GameId))) return false;
            reviews.Add(review.Id, review);
            pairs.Add((review.UserId, review.GameId));
            return true;
        }
    }
    public Review? Find(string id) { lock (gate) return reviews.GetValueOrDefault(id); }
    public IReadOnlyList<Review> All() { lock (gate) return reviews.Values.ToArray(); }
    public Review? Edit(string id, string actorId, string? text, DateTimeOffset now)
    {
        lock (gate)
        {
            if (!reviews.TryGetValue(id, out var review)) return null;
            var updated = review.Edit(actorId, text, now);
            reviews[id] = updated;
            return updated;
        }
    }
}
