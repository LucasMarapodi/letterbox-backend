namespace Tcc.Domain.Identity;

public sealed record UserProfile
{
    public string DisplayName { get; }
    public string Bio { get; }
    public string? AvatarUrl { get; }
    public string? Country { get; }
    public string? State { get; }
    public DateTimeOffset UpdatedAt { get; }

    private UserProfile(string displayName, string bio, string? avatarUrl, DateTimeOffset updatedAt, string? country, string? state)
    {
        DisplayName = displayName;
        Bio = bio;
        AvatarUrl = avatarUrl;
        Country = country;
        State = state;
        UpdatedAt = updatedAt;
    }

    public static UserProfile Create(string? displayName, string bio = "", string? avatarUrl = null, DateTimeOffset? updatedAt = null,
        string? country = null, string? state = null)
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
        return new UserProfile(name, bio, avatarUrl, (updatedAt ?? DateTimeOffset.UtcNow).ToUniversalTime(), Location(country), Location(state));
    }

    public UserProfile Apply(ProfileChange change, DateTimeOffset? now = null) => Create(change.DisplayName ?? DisplayName,
        change.Bio ?? Bio, change.AvatarSpecified ? change.AvatarUrl : AvatarUrl, now,
        change.CountrySpecified ? change.Country : Country, change.StateSpecified ? change.State : State);

    private static string? Location(string? value)
    {
        value = value?.Trim();
        if (value?.Length > 100) throw new DomainValidationException("Localização inválida.");
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
