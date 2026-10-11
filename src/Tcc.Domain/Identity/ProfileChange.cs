namespace Tcc.Domain.Identity;

public sealed record ProfileChange(string? DisplayName, string? Bio, bool AvatarSpecified, string? AvatarUrl);
