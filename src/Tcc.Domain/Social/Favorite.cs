using Tcc.Domain.Identity;

namespace Tcc.Domain.Social;

public enum FavoriteKind { Game, Review }
public sealed record Favorite
{
    public string Id { get; }
    public string UserId { get; }
    public FavoriteKind Kind { get; }
    public string TargetId { get; }
    public Favorite(string id, string userId, FavoriteKind kind, string targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
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
