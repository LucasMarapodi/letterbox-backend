using Tcc.Domain.Identity;

namespace Tcc.Domain.Opinions;

public sealed record Review
{
    public int Id { get; private set; }
    public int UserId { get; }
    public int GameId { get; }
    public string Text { get; }
    public string? Title { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; }
    public Rating? Evaluation { get; init; }

    private Review(int id, int userId, int gameId, string text, DateTimeOffset createdAt, DateTimeOffset? updatedAt, string? title)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        Id = id; UserId = userId; GameId = gameId; Text = text; Title = title;
        CreatedAt = createdAt.ToUniversalTime(); UpdatedAt = updatedAt?.ToUniversalTime();
    }

    public static Review Create(int id, int userId, int gameId, string? text, DateTimeOffset now, string? title = null)
    {
        var value = ValidateText(text);
        return new(id, userId, gameId, value, now, null, ValidateTitle(title, value));
    }

    public static Review CreateRated(int userId, int gameId, string? text, double? rating, DateTimeOffset now, string? title = null) =>
        Create(0, userId, gameId, text, now, title) with { Evaluation = new Rating(0, userId, gameId,
            RatingValue.Create(rating ?? throw new DomainValidationException("Informe a nota.")), now, now) };

    public Review EditRated(int actorId, bool textSpecified, string? text, double? rating, DateTimeOffset now, bool titleSpecified = false, string? title = null)
    {
        var edited = Edit(actorId, textSpecified ? text : Text, now, titleSpecified, title);
        var value = rating is double supplied ? RatingValue.Create(supplied) : Evaluation?.Value
            ?? throw new DomainValidationException("Informe a nota da review legada.");
        return edited with { Evaluation = new Rating(Evaluation?.Id ?? 0, UserId, GameId, value, Evaluation?.CreatedAt ?? now, now) };
    }

    public Review Edit(int actorId, string? text, DateTimeOffset now, bool titleSpecified = false, string? title = null)
    {
        if (actorId != UserId) throw new ReviewOwnershipException();
        var value = ValidateText(text);
        return new Review(Id, UserId, GameId, value, CreatedAt, now, ValidateTitle(titleSpecified ? title : Title, value)) { Evaluation = Evaluation };
    }

    private static string? ValidateTitle(string? title, string text)
    {
        var value = title?.Trim();
        if (value?.Length > 150) throw new ReviewTitleException(ReviewTitleFailureKind.TooLong);
        if (string.IsNullOrEmpty(value))
        {
            if (text.Length > 0) throw new ReviewTitleException(ReviewTitleFailureKind.Required);
            return null;
        }
        return value;
    }

    private static string ValidateText(string? text)
    {
        var value = text?.Trim() ?? "";
        return value.Length > 10_000
            ? throw new DomainValidationException("Texto de review inválido.") : value;
    }
}

public sealed class ReviewOwnershipException() : Exception("Somente o autor pode editar a review.");
public enum ReviewTitleFailureKind { Required, TooLong }
public sealed class ReviewTitleException(ReviewTitleFailureKind kind) : Exception(kind == ReviewTitleFailureKind.Required
    ? "Informe o título quando houver descrição." : "Título deve ter até 150 caracteres.")
{
    public ReviewTitleFailureKind Kind { get; } = kind;
}
