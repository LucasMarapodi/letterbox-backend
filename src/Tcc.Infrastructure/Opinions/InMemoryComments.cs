using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Social;

namespace Tcc.Infrastructure.Opinions;

public sealed class InMemoryComments : IComments
{
    private readonly Lock gate;
    private readonly InMemoryPublicationScope publications;
    private readonly Dictionary<string, Comment> comments = new(StringComparer.Ordinal);
    public InMemoryComments(InMemoryPublicationScope publications)
    {
        gate = publications.Gate;
        this.publications = publications;

    }
    public IReadOnlyList<Comment> ForReview(int reviewId)
    {
        lock (gate) return comments.Values.Where(comment => comment.ReviewId == reviewId).ToArray();
    }
    public IReadOnlyList<Comment> InWindow(DateTimeOffset from, DateTimeOffset to)
    { lock (gate) return comments.Values.Where(item => item.CreatedAt >= from && item.CreatedAt <= to).ToArray(); }
    public void Add(Comment comment)
    {
        lock (gate)
        {
            if (comments.ContainsKey(comment.Id)) throw new InvalidOperationException("Comentário duplicado.");
            publications.OnRollback(() => comments.Remove(comment.Id));
            comments.Add(comment.Id, comment);
        }
    }
}
