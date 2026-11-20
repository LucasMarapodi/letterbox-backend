namespace Tcc.Domain.Identity;

public sealed record UserProfile
{
    public string DisplayName { get; }
    public string Bio { get; }
    public string? AvatarUrl { get; }
    public string? BannerUrl { get; }
    public int BannerPositionX { get; }
    public int BannerPositionY { get; }
    public string? Country { get; }
    public string? State { get; }
    public DateTimeOffset UpdatedAt { get; }

    private UserProfile(string displayName, string bio, string? avatarUrl, DateTimeOffset updatedAt, string? country, string? state, string? bannerUrl, int bannerPositionX, int bannerPositionY)
    {
        DisplayName = displayName;
        Bio = bio;
        AvatarUrl = avatarUrl;
        BannerUrl = bannerUrl;
        BannerPositionX = bannerPositionX;
        BannerPositionY = bannerPositionY;
        Country = country;
        State = state;
        UpdatedAt = updatedAt;
    }

    public static UserProfile Create(string? displayName, string bio = "", string? avatarUrl = null, DateTimeOffset? updatedAt = null,
        string? country = null, string? state = null, string? bannerUrl = null, int bannerPositionX = 50, int bannerPositionY = 50)
    {
        var name = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new DomainValidationException("Nome inválido.");
        if (bio.Length > 2000)
            throw new DomainValidationException("Biografia inválida.");
        ValidateImage(avatarUrl);
        ValidateImage(bannerUrl);
        ValidatePosition(bannerPositionX, bannerPositionY);
        return new UserProfile(name, bio, avatarUrl, (updatedAt ?? DateTimeOffset.UtcNow).ToUniversalTime(), Location(country), Location(state), bannerUrl, bannerPositionX, bannerPositionY);
    }

    public UserProfile Apply(ProfileChange change, DateTimeOffset? now = null)
    {
        var avatar = change.AvatarSpecified ? (string.IsNullOrWhiteSpace(change.AvatarUrl) ? null : change.AvatarUrl) : AvatarUrl;
        // Historical URLs may round-trip unchanged; only a new image must meet the current policy.
        if (avatar != AvatarUrl) ValidateImage(avatar);
        var banner = change.BannerSpecified ? (string.IsNullOrWhiteSpace(change.BannerUrl) ? null : change.BannerUrl) : BannerUrl;
        if (banner != BannerUrl) ValidateImage(banner);
        var x = change.BannerSpecified && banner is null ? 50 : change.BannerPositionX ?? BannerPositionX;
        var y = change.BannerSpecified && banner is null ? 50 : change.BannerPositionY ?? BannerPositionY;
        ValidatePosition(change.BannerPositionX ?? x, change.BannerPositionY ?? y);
        var updated = Create(change.DisplayName ?? DisplayName, change.Bio?.Trim() ?? Bio, updatedAt: now);
        // Preserve untouched historical values exactly; validate a changed final pair together.
        var country = change.CountrySpecified && change.Country != Country ? Location(change.Country) : Country;
        var state = change.StateSpecified && change.State != State ? Location(change.State) : State;
        if (country != Country || state != State) ProfileLocations.Validate(country, state);
        return new(updated.DisplayName, updated.Bio, avatar, updated.UpdatedAt, country, state, banner, x, y);
    }

    private static void ValidatePosition(int x, int y)
    {
        if (x is < 0 or > 100 || y is < 0 or > 100)
            throw new DomainValidationException("Posição do banner deve estar entre 0 e 100.");
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
