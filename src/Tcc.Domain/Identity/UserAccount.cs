namespace Tcc.Domain.Identity;

public sealed class UserAccount
{
    public string Id { get; }
    public string Username { get; }
    public Email Email { get; }
    public string PasswordHash { get; }
    public UserProfile Profile { get; }

    public UserAccount(string id, string username, Email email, string passwordHash, UserProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(profile);
        Id = id;
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Profile = profile;
    }

    public UserAccount UpdateProfile(ProfileChange change) => new(Id, Username, Email, PasswordHash, Profile.Apply(change));
}
