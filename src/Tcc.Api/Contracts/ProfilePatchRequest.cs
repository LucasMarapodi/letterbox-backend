using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Tcc.Domain.Identity;

namespace Tcc.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ProfilePatchRequest : IValidatableObject
{
    private string? displayName;
    private string? bio;
    private string? avatarUrl;
    private bool displayNameSpecified;
    private bool bioSpecified;
    private bool avatarSpecified;
    private string? country;
    private string? state;
    private bool countrySpecified;
    private bool stateSpecified;
    private IReadOnlyList<int>? favoriteGameIds;
    private bool favoritesSpecified;
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public IReadOnlyList<int>? FavoriteGameIds { get => favoriteGameIds; set { favoriteGameIds = value; favoritesSpecified = true; } }
    public string? Country { get => country; set { country = value; countrySpecified = true; } }
    public string? State { get => state; set { state = value; stateSpecified = true; } }

    public string? DisplayName
    {
        get => displayName;
        set { displayName = value; displayNameSpecified = true; }
    }

    public string? Bio
    {
        get => bio;
        set { bio = value; bioSpecified = true; }
    }

    public string? AvatarUrl
    {
        get => avatarUrl;
        set { avatarUrl = value; avatarSpecified = true; }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (displayNameSpecified && DisplayName is null)
            yield return new ValidationResult("Nome não pode ser null.", [nameof(DisplayName)]);
        if (favoritesSpecified && FavoriteGameIds is null)
            yield return new ValidationResult("Favoritos não podem ser null; envie [] para limpar.", [nameof(FavoriteGameIds)]);
    }

    public ProfileChange ToChange() => new(DisplayName, bioSpecified ? Bio ?? "" : null, avatarSpecified, AvatarUrl, countrySpecified, Country, stateSpecified, State);
}
