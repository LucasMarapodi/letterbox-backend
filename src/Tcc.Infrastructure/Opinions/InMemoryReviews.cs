using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Social;

namespace Tcc.Infrastructure.Opinions;

public sealed class InMemoryReviews : IReviews
{
    private readonly Lock gate;
    private readonly InMemoryPublicationScope publications;
    private readonly Dictionary<string, Review> reviews = new(StringComparer.Ordinal);
    private readonly HashSet<(int User, string Game)> pairs = [];

    public InMemoryReviews(InMemoryPublicationScope publications)
    {
        gate = publications.Gate;
        this.publications = publications;

    }

    public bool TryAdd(Review review)
    {
        lock (gate)
        {
            if (reviews.ContainsKey(review.Id) || pairs.Contains((review.UserId, review.GameId))) return false;
            publications.OnRollback(() => { reviews.Remove(review.Id); pairs.Remove((review.UserId, review.GameId)); });
            reviews.Add(review.Id, review);
            pairs.Add((review.UserId, review.GameId));
            return true;
        }
    }
    public Review? Find(string id) { lock (gate) return reviews.GetValueOrDefault(id); }
    public IReadOnlyList<Review> All() { lock (gate) return reviews.Values.ToArray(); }
    public Review? Edit(string id, int actorId, string? text, DateTimeOffset now)
    {
        lock (gate)
        {
            if (!reviews.TryGetValue(id, out var review)) return null;
            var updated = review.Edit(actorId, text, now);
            publications.OnRollback(() => reviews[id] = review);
            reviews[id] = updated;
            return updated;
        }
    }
}
