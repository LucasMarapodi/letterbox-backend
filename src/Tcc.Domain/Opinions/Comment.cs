using Tcc.Domain.Identity;

namespace Tcc.Domain.Opinions;

public sealed record Comment
{
    public string Id { get; }
    public string UserId { get; }
    public string ReviewId { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }
    private Comment(string id, string userId, string reviewId, string text, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewId);
        Id = id; UserId = userId; ReviewId = reviewId; Text = text; CreatedAt = now.ToUniversalTime();
    }
    public static Comment Create(string id, string userId, string reviewId, string? text, DateTimeOffset now)
    {
        var value = text?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2000) throw new DomainValidationException("Comentário inválido.");
        return new(id, userId, reviewId, value, now);
    }
}
