using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tcc.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ProfileCompletionRequest
{
    [Required] public string? Country { get; init; }
    [Required] public string? State { get; init; }
    [Required] public string? Bio { get; init; }
    [Required] public IReadOnlyList<int>? FavoriteGameIds { get; init; }
    private string? avatarUrl;
    public string? AvatarUrl { get => avatarUrl; set { avatarUrl = value; AvatarSpecified = true; } }
    [JsonIgnore] public bool AvatarSpecified { get; private set; }
}
