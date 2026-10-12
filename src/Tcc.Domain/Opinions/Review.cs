using Tcc.Domain.Identity;

namespace Tcc.Domain.Opinions;

public sealed record Review
{
    public string Id { get; }
    public string UserId { get; }
    public string GameId { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; }

    private Review(string id, string userId, string gameId, string text, DateTimeOffset createdAt, DateTimeOffset? updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        Id = id; UserId = userId; GameId = gameId; Text = text;
        CreatedAt = createdAt.ToUniversalTime(); UpdatedAt = updatedAt?.ToUniversalTime();
    }

    public static Review Create(string id, string userId, string gameId, string? text, DateTimeOffset now) =>
        new(id, userId, gameId, ValidateText(text), now, null);

    private static string ValidateText(string? text)
    {
        var value = text?.Trim();
        return string.IsNullOrWhiteSpace(value) || value.Length > 10_000
            ? throw new DomainValidationException("Texto de review inválido.") : value;
    }
}
