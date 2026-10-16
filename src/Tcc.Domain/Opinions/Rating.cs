using Tcc.Domain.Identity;

namespace Tcc.Domain.Opinions;

public sealed record RatingValue
{
    public double Value { get; }
    private RatingValue(double value) => Value = value;
    public static RatingValue Create(double value) => double.IsFinite(value) && value >= 0 && value <= 5
        ? new(value) : throw new DomainValidationException("Nota inválida.");
}

public sealed record Rating(int Id, int UserId, int GameId, RatingValue Value, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
