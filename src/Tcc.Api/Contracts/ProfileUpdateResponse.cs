using Tcc.Application.Identity;

namespace Tcc.Api.Contracts;

// Flat user fields preserve existing PATCH consumers; private completion belongs only here.
public sealed record ProfileUpdateResponse(int Id, string Username, string DisplayName, string Bio, string? AvatarUrl, ProfileCompletionDetails Completion)
{
    public static ProfileUpdateResponse From(ProfileSaveResult saved) =>
        new(saved.User.Id, saved.User.Username, saved.User.Profile.DisplayName, saved.User.Profile.Bio, saved.User.Profile.AvatarUrl, saved.Completion);
}
