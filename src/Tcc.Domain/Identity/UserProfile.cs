namespace Tcc.Domain.Identity;

public sealed record UserProfile
{
    public string DisplayName { get; }
    public string Bio { get; }
    public string? AvatarUrl { get; }

    private UserProfile(string displayName, string bio, string? avatarUrl)
    {
        DisplayName = displayName;
        Bio = bio;
        AvatarUrl = avatarUrl;
    }

    public static UserProfile Create(string? displayName, string bio = "", string? avatarUrl = null)
    {
        var name = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new DomainValidationException("Nome inválido.");
        if (bio.Length > 2000)
            throw new DomainValidationException("Biografia inválida.");
        if (avatarUrl is not null && (avatarUrl.Length > 2048 ||
            !Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            throw new DomainValidationException("Avatar inválido.");
        return new UserProfile(name, bio, avatarUrl);
    }

    public UserProfile Apply(ProfileChange change) => Create(change.DisplayName ?? DisplayName,
        change.Bio ?? Bio, change.AvatarSpecified ? change.AvatarUrl : AvatarUrl);
}
