namespace Tcc.Domain.Identity;

public static class ProfileCompletionPolicy
{
    public static bool IsComplete(string? country, string? state, string? bio, IEnumerable<int> favoriteGameIds) =>
        !string.IsNullOrWhiteSpace(country) && !string.IsNullOrWhiteSpace(state) && !string.IsNullOrWhiteSpace(bio)
        && favoriteGameIds.Where(id => id > 0).Distinct().Take(3).Count() == 3;
}
