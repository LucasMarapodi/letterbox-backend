using Tcc.Domain.Identity;

namespace Tcc.Domain.Tests;

public sealed class IdentityTests
{
    [Fact]
    public void Email_normalizes_and_has_value_equality()
    {
        Assert.Equal(Email.Create(" Person@EXAMPLE.COM "), Email.Create("person@example.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bad")]
    [InlineData("Name <person@example.com>")]
    [InlineData("a b@example.com")]
    public void Email_rejects_invalid_mailboxes(string? value) =>
        Assert.Throws<DomainValidationException>(() => Email.Create(value));

    [Fact]
    public void Profile_enforces_limits_without_mutating_the_previous_snapshot()
    {
        var profile = UserProfile.Create("Ana", "Bio", "https://example.com/a");
        Assert.Throws<DomainValidationException>(() => profile.Apply(new ProfileChange(" ", "New", true, null)));
        Assert.Throws<DomainValidationException>(() => profile.Apply(new ProfileChange(new string('n', 101), null, false, null)));
        Assert.Throws<DomainValidationException>(() => profile.Apply(new ProfileChange(null, new string('b', 2001), false, null)));
        Assert.Throws<DomainValidationException>(() => profile.Apply(new ProfileChange(null, null, true, "file:///tmp/a")));
        var updated = profile.Apply(new ProfileChange("Edited", null, true, null));
        Assert.Equal("Bio", updated.Bio);
        Assert.Null(updated.AvatarUrl);
        Assert.Equal("Ana", profile.DisplayName);
        Assert.Equal("https://example.com/a", profile.AvatarUrl);
    }

    [Fact]
    public void Account_update_preserves_identity_and_credentials()
    {
        var account = new UserAccount("id", "user-id", Email.Create("a@example.com"), "verifier", UserProfile.Create("A"));
        var updated = account.UpdateProfile(new ProfileChange("B", "", false, null));
        Assert.Equal(account.Id, updated.Id);
        Assert.Equal(account.Username, updated.Username);
        Assert.Equal(account.Email, updated.Email);
        Assert.Equal(account.PasswordHash, updated.PasswordHash);
        Assert.Equal("A", account.Profile.DisplayName);
        Assert.Equal("B", updated.Profile.DisplayName);
    }
}
