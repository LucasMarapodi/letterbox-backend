using Tcc.Domain.Identity;

namespace Tcc.Domain.Tests;

public sealed class AvatarHttpsTests
{
    [Theory]
    [InlineData("http://example.com/avatar.png")]
    [InlineData("ftp://example.com/a")]
    [InlineData("/avatar.png")]
    [InlineData("data:image/png;base64,fixture")]
    public void Creation_and_replacement_require_absolute_https(string url)
    {
        Assert.Throws<DomainValidationException>(() => UserProfile.Create("User", avatarUrl: url));
        var profile = UserProfile.Create("User", avatarUrl: "https://example.com/original");
        Assert.Throws<DomainValidationException>(() => profile.Apply(new(null, null, true, url)));
        Assert.Equal("https://example.com/original", profile.AvatarUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Explicit_removal_and_omission_preserve_existing_semantics(string? value)
    {
        var profile = UserProfile.Create("User", avatarUrl: "https://example.com/original");
        Assert.Equal(profile.AvatarUrl, profile.Apply(new("Renamed", "New bio", false, null)).AvatarUrl);
        Assert.Null(profile.Apply(new(null, null, true, value)).AvatarUrl);
        Assert.Equal("https://example.com/new", profile.Apply(new(null, null, true, "https://example.com/new")).AvatarUrl);
    }

    [Fact]
    public void Avatar_length_limit_is_preserved()
    {
        var url = "https://example.com/" + new string('a', 2048 - "https://example.com/".Length);
        Assert.Equal(url, UserProfile.Create("User", avatarUrl: url).AvatarUrl);
        Assert.Throws<DomainValidationException>(() => UserProfile.Create("User", avatarUrl: url + "x"));
    }
}
