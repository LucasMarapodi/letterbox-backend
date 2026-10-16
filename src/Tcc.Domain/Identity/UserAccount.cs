namespace Tcc.Domain.Identity;

public sealed class UserAccount
{
    private UserProfile? profile;
    public int Id { get; private set; }
    public string Username { get; private set; }
    public Email Email { get; private set; }
    public string PasswordHash { get; private set; }
    public UserProfile Profile { get => profile ?? throw new InvalidOperationException("Perfil não materializado."); private set => profile = value; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private UserAccount(int id, string username, Email email, string passwordHash, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        if (id > 0) ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentNullException.ThrowIfNull(email);
        Id = id;
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    public UserAccount(int id, string username, Email email, string passwordHash, UserProfile profile,
        DateTimeOffset? createdAt = null, DateTimeOffset? updatedAt = null)
        : this(id, username, email, passwordHash, createdAt ?? DateTimeOffset.UtcNow, updatedAt ?? createdAt ?? DateTimeOffset.UtcNow)
    { ArgumentNullException.ThrowIfNull(profile); Profile = profile; }

    public UserAccount UpdateProfile(ProfileChange change, DateTimeOffset? now = null) =>
        new(Id, Username, Email, PasswordHash, Profile.Apply(change, now), CreatedAt, now ?? DateTimeOffset.UtcNow);
}
