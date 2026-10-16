using Tcc.Domain.Identity;

namespace Tcc.Domain.Tests;

public sealed class ProfileLocationTests
{
    [Theory]
    [InlineData("Brasil", "Hubei")]
    [InlineData(null, "São Paulo")]
    [InlineData("Other", null)]
    public void Changed_location_must_form_a_supported_pair(string? country, string? state)
    {
        var profile = UserProfile.Create("Name");
        Assert.Throws<DomainValidationException>(() => profile.Apply(new(null,null,false,null,true,country,true,state)));
    }

    [Fact]
    public void Legacy_location_is_preserved_when_only_unrelated_fields_change()
    {
        var profile = UserProfile.Create("Name", country: "Legacy", state: "Region");
        var updated = profile.Apply(new("Changed",null,false,null));
        Assert.Equal(profile.Country, updated.Country);
        Assert.Equal(profile.State, updated.State);
    }
}
