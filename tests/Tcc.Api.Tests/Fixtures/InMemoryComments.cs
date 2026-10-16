using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Social;

namespace Tcc.Infrastructure.Opinions;

public sealed class InMemoryComments(InMemoryPublicationScope publications) : IComments, ICommentLikes
{
    private readonly Dictionary<int, Comment> comments = [];
    private readonly Dictionary<(int User, int Comment), CommentLike> likes = [];
    private int next = 5000;
    public IReadOnlyList<Comment> ForReview(int reviewId) { lock (publications.Gate) return comments.Values.Where(item => item.ReviewId == reviewId).ToArray(); }
    public void Add(Comment comment)
    {
        lock (publications.Gate)
        {
            if (comments.ContainsKey(comment.Id)) throw new InvalidOperationException("Comentário duplicado.");
            publications.OnRollback(() => comments.Remove(comment.Id));
            comments.Add(comment.Id, comment);
        }
    }
    public Task<Comment> AddAsync(Comment comment, CancellationToken ct = default, Func<Comment, IPreparedPublication>? prepare = null)
    {
        ct.ThrowIfCancellationRequested();
        lock (publications.Gate)
        {
            var saved = Comment.Create(comment.Id == 0 ? ++next : comment.Id, comment.UserId, comment.ReviewId, comment.Text, comment.CreatedAt, comment.ParentCommentId);
            if (saved.ParentCommentId is int parentId)
            {
                var parent = comments[parentId];
                saved = Comment.Reply(saved.Id, saved.UserId, saved.ReviewId, saved.Text, saved.CreatedAt, parent);
            }
            using var publication = prepare?.Invoke(saved);
            Add(saved);
            try { publication?.Commit(); return Task.FromResult(saved); }
            catch { comments.Remove(saved.Id); throw; }
        }
    }
    public Task<Comment?> FindAsync(int id, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (publications.Gate) return Task.FromResult(comments.GetValueOrDefault(id)); }
    public Task<IReadOnlyList<CommentWithLikes>> ForReviewAsync(int reviewId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (publications.Gate) return Task.FromResult<IReadOnlyList<CommentWithLikes>>(ForReview(reviewId).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new CommentWithLikes(item, likes.Keys.Count(key => key.Comment == item.Id))).ToArray());
    }
    public Task<IReadOnlyList<CommentActivitySummary>> ActivityAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (publications.Gate) return Task.FromResult<IReadOnlyList<CommentActivitySummary>>(comments.Values.Where(item => item.CreatedAt >= from && item.CreatedAt <= to)
            .GroupBy(item => item.ReviewId).Select(group => new CommentActivitySummary(group.Key, group.Count(), group.Max(item => item.CreatedAt))).ToArray());
    }
    public Task SetAsync(int userId, int commentId, DateTimeOffset now, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (publications.Gate) likes.TryAdd((userId, commentId), new(userId, commentId, now)); return Task.CompletedTask; }
    public Task RemoveAsync(int userId, int commentId, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (publications.Gate) likes.Remove((userId, commentId)); return Task.CompletedTask; }
    public Task<bool> HasAsync(int userId, int commentId, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); lock (publications.Gate) return Task.FromResult(likes.ContainsKey((userId, commentId))); }
}
