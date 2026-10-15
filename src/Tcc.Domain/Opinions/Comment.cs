using Tcc.Domain.Identity;

namespace Tcc.Domain.Opinions;

public sealed record Comment
{
    public int Id { get; private set; }
    public int UserId { get; }
    public int ReviewId { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }
    public int? ParentCommentId { get; }
    private Comment(int id, int userId, int reviewId, string text, DateTimeOffset createdAt, int? parentCommentId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reviewId);
        if (parentCommentId is <= 0 || id > 0 && parentCommentId == id) throw new DomainValidationException("Pai de comentário inválido.");
        Id = id; UserId = userId; ReviewId = reviewId; Text = text; CreatedAt = createdAt.ToUniversalTime(); ParentCommentId = parentCommentId;
    }
    public static Comment Create(int id, int userId, int reviewId, string? text, DateTimeOffset now, int? parentCommentId = null)
    {
        var value = text?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2000) throw new DomainValidationException("Comentário inválido.");
        return new(id, userId, reviewId, value, now, parentCommentId);
    }
    public static Comment Reply(int id, int userId, int reviewId, string? text, DateTimeOffset now, Comment parent)
    {
        if (parent.Id <= 0 || parent.ReviewId != reviewId || parent.ParentCommentId is not null)
            throw new DomainValidationException("A resposta deve apontar para um comentário principal da mesma review.");
        return Create(id, userId, reviewId, text, now, parent.Id);
    }
}
