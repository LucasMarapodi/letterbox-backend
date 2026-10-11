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
        if (bioSpecified && Bio is null)
            yield return new ValidationResult("Biografia não pode ser null.", [nameof(Bio)]);
    }

    public ProfileChange ToChange() => new(DisplayName, Bio, avatarSpecified, AvatarUrl);
}
