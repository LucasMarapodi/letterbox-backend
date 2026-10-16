using Tcc.Domain.Identity;

namespace Tcc.Domain.Tests;

public sealed class ProfileCompletionPolicyTests
{
    [Theory]
    [InlineData("Country", "State", "Bio", 0, false)]
    [InlineData("Country", "State", "Bio", 2, false)]
    [InlineData("Country", "State", "Bio", 3, true)]
    [InlineData("Country", "State", "Bio", 4, true)]
    [InlineData(null, "State", "Bio", 3, false)]
    [InlineData("Country", " ", "Bio", 3, false)]
    [InlineData("Country", "State", " ", 3, false)]
    public void Completion_is_derived_from_required_fields_and_distinct_games(string? country, string? state, string? bio, int count, bool expected)
    {
        Assert.Equal(expected, ProfileCompletionPolicy.IsComplete(country, state, bio, Enumerable.Range(1,count).Concat(Enumerable.Range(1,count))));
    }
}
