using Tcc.Domain.Identity;

namespace Tcc.Domain.Social;

public enum FavoriteKind { Game, Review }
public sealed record Favorite
{
    public int Id { get; }
    public int UserId { get; }
    public FavoriteKind Kind { get; }
    public int TargetId { get; }
    public Favorite(int id, int userId, FavoriteKind kind, int targetId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        if (!Enum.IsDefined(kind)) throw new DomainValidationException("Tipo de favorito inválido.");
        Id = id; UserId = userId; Kind = kind; TargetId = targetId;
    }
    public static FavoriteKind ParseKind(string kind) => kind switch
    {
        "game" => FavoriteKind.Game,
        "review" => FavoriteKind.Review,
        _ => throw new DomainValidationException("Tipo de favorito inválido.")
    };
}
