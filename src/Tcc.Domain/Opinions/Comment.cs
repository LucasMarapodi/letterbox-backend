using Tcc.Domain.Identity;

namespace Tcc.Domain.Opinions;

public sealed record Comment
{
    public string Id { get; }
    public int UserId { get; }
    public int ReviewId { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }
    private Comment(string id, int userId, int reviewId, string text, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reviewId);
        Id = id; UserId = userId; ReviewId = reviewId; Text = text; CreatedAt = now.ToUniversalTime();
    }
    public static Comment Create(string id, int userId, int reviewId, string? text, DateTimeOffset now)
    {
        var value = text?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2000) throw new DomainValidationException("Comentário inválido.");
        return new(id, userId, reviewId, value, now);
    }
}
