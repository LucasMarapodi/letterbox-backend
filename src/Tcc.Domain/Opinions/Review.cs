using Tcc.Domain.Identity;

namespace Tcc.Domain.Opinions;

public sealed record Review
{
    public int Id { get; private set; }
    public int UserId { get; }
    public int GameId { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; }
    public Rating? Evaluation { get; init; }

    private Review(int id, int userId, int gameId, string text, DateTimeOffset createdAt, DateTimeOffset? updatedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        Id = id; UserId = userId; GameId = gameId; Text = text;
        CreatedAt = createdAt.ToUniversalTime(); UpdatedAt = updatedAt?.ToUniversalTime();
    }

    public static Review Create(int id, int userId, int gameId, string? text, DateTimeOffset now) =>
        new(id, userId, gameId, ValidateText(text), now, null);

    public static Review CreateRated(int userId, int gameId, string? text, double? rating, DateTimeOffset now) =>
        Create(0, userId, gameId, text, now) with { Evaluation = new Rating(0, userId, gameId,
            RatingValue.Create(rating ?? throw new DomainValidationException("Informe a nota.")), now, now) };

    public Review EditRated(int actorId, bool textSpecified, string? text, double? rating, DateTimeOffset now)
    {
        var edited = Edit(actorId, textSpecified ? text : Text, now);
        var value = rating is double supplied ? RatingValue.Create(supplied) : Evaluation?.Value
            ?? throw new DomainValidationException("Informe a nota da review legada.");
        return edited with { Evaluation = new Rating(Evaluation?.Id ?? 0, UserId, GameId, value, Evaluation?.CreatedAt ?? now, now) };
    }

    public Review Edit(int actorId, string? text, DateTimeOffset now)
    {
        if (actorId != UserId) throw new ReviewOwnershipException();
        return new Review(Id, UserId, GameId, ValidateText(text), CreatedAt, now) { Evaluation = Evaluation };
    }

    private static string ValidateText(string? text)
    {
        var value = text?.Trim() ?? "";
        return value.Length > 10_000
            ? throw new DomainValidationException("Texto de review inválido.") : value;
    }
}

public sealed class ReviewOwnershipException() : Exception("Somente o autor pode editar a review.");
