namespace Tcc.Domain.Identity;

public sealed record UserProfile
{
    public string DisplayName { get; }
    public string Bio { get; }
    public string? AvatarUrl { get; }
    public string? BannerUrl { get; }
    public string? Country { get; }
    public string? State { get; }
    public DateTimeOffset UpdatedAt { get; }

    private UserProfile(string displayName, string bio, string? avatarUrl, DateTimeOffset updatedAt, string? country, string? state, string? bannerUrl)
    {
        DisplayName = displayName;
        Bio = bio;
        AvatarUrl = avatarUrl;
        BannerUrl = bannerUrl;
        Country = country;
        State = state;
        UpdatedAt = updatedAt;
    }

    public static UserProfile Create(string? displayName, string bio = "", string? avatarUrl = null, DateTimeOffset? updatedAt = null,
        string? country = null, string? state = null, string? bannerUrl = null)
    {
        var name = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new DomainValidationException("Nome inválido.");
        if (bio.Length > 2000)
            throw new DomainValidationException("Biografia inválida.");
        ValidateImage(avatarUrl);
        ValidateImage(bannerUrl);
        return new UserProfile(name, bio, avatarUrl, (updatedAt ?? DateTimeOffset.UtcNow).ToUniversalTime(), Location(country), Location(state), bannerUrl);
    }

    public UserProfile Apply(ProfileChange change, DateTimeOffset? now = null)
    {
        var avatar = change.AvatarSpecified ? (string.IsNullOrWhiteSpace(change.AvatarUrl) ? null : change.AvatarUrl) : AvatarUrl;
        // Historical URLs may round-trip unchanged; only a new image must meet the current policy.
        if (avatar != AvatarUrl) ValidateImage(avatar);
        var banner = change.BannerSpecified ? (string.IsNullOrWhiteSpace(change.BannerUrl) ? null : change.BannerUrl) : BannerUrl;
        if (banner != BannerUrl) ValidateImage(banner);
        var updated = Create(change.DisplayName ?? DisplayName, change.Bio?.Trim() ?? Bio, updatedAt: now);
        // Preserve untouched historical values exactly; validate a changed final pair together.
        var country = change.CountrySpecified && change.Country != Country ? Location(change.Country) : Country;
        var state = change.StateSpecified && change.State != State ? Location(change.State) : State;
        if (country != Country || state != State) ProfileLocations.Validate(country, state);
        return new(updated.DisplayName, updated.Bio, avatar, updated.UpdatedAt, country, state, banner);
    }

    private static void ValidateImage(string? value)
    {
        if (value is not null && (value.Length > 2048 ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainValidationException("Imagem deve ser uma URL HTTPS absoluta de até 2048 caracteres.");
    }

    private static string? Location(string? value)
    {
        value = value?.Trim();
        if (value?.Length > 100) throw new DomainValidationException("Localização inválida.");
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
