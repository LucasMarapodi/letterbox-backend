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
        var now = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
        comments.Add("comment-b", Comment.Create("comment-b", "user-1", "review-fixture", "Segundo comentário.", now));
        comments.Add("comment-a", Comment.Create("comment-a", "user-1", "review-fixture", "Primeiro no empate.", now));
        comments.Add("comment-c", Comment.Create("comment-c", "user-1", "review-fixture", "Mais antigo.", now.AddDays(-1)));
    }
    public IReadOnlyList<Comment> ForReview(string reviewId)
    {
        lock (gate) return comments.Values.Where(comment => comment.ReviewId == reviewId).ToArray();
    }
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
