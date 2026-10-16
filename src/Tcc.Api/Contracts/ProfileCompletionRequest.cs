using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tcc.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ProfileCompletionRequest
{
    public string? Country { get; init; }
    public string? State { get; init; }
    public string? Bio { get; init; }
    [Required, JsonNumberHandling(JsonNumberHandling.Strict)] public IReadOnlyList<int>? FavoriteGameIds { get; init; }
    private string? avatarUrl;
    public string? AvatarUrl { get => avatarUrl; set { avatarUrl = value; AvatarSpecified = true; } }
    [JsonIgnore] public bool AvatarSpecified { get; private set; }
}
