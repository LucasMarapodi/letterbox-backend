namespace Tcc.Domain.Identity;

public sealed record ProfileChange(string? DisplayName, string? Bio, bool AvatarSpecified, string? AvatarUrl,
    bool CountrySpecified = false, string? Country = null, bool StateSpecified = false, string? State = null);
