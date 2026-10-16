using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Tcc.Domain.Identity;

namespace Tcc.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterRequest([Required] string? Name, [Required] string? Email, [Required] string? Password);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoginRequest([Required] string? Email, [Required] string? Password);

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, PublicUserResponse User);

public sealed record PublicUserResponse(int Id, string Username, string DisplayName, string Bio, string? AvatarUrl)
{
    public static PublicUserResponse From(UserAccount account) =>
        new(account.Id, account.Username, account.Profile.DisplayName, account.Profile.Bio, account.Profile.AvatarUrl);
}
