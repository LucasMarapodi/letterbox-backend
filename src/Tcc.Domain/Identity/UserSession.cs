namespace Tcc.Domain.Identity;

public sealed class UserSession
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    private UserSession(int id, int userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt, DateTimeOffset? revokedAt)
    { Id = id; UserId = userId; TokenHash = tokenHash; CreatedAt = createdAt; ExpiresAt = expiresAt; RevokedAt = revokedAt; }
    public UserSession(int userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
        : this(0, userId, tokenHash, createdAt.ToUniversalTime(), expiresAt.ToUniversalTime(), null)
    {
        if (userId <= 0 || tokenHash.Length != 64 || tokenHash.Any(character => !char.IsAsciiHexDigit(character)) || expiresAt <= createdAt)
            throw new DomainValidationException("Sessão inválida.");
    }
    public override string ToString() => "User session (verifier redacted)";
}
