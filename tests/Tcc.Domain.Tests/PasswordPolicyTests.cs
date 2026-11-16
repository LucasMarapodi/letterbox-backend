using Tcc.Domain.Identity;

namespace Tcc.Domain.Tests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("      ")]
    [InlineData("Abc1!")]
    [InlineData("abc1!x")]
    [InlineData("Abcd!x")]
    [InlineData("Abc12x")]
    [InlineData("Abc1 x")]
    [InlineData("Abc1\tx")]
    [InlineData("Abc1\u0001x")]
    public void Rejects_each_missing_requirement(string? password) =>
        Assert.Throws<DomainValidationException>(() => PasswordPolicy.Validate(password));

    [Theory]
    [InlineData("Abc1!x")]
    [InlineData("ABC1!X")]
    [InlineData("Abc1!longer")]
    [InlineData(" Abc1!x ")]
    [InlineData(" Ab1! ")]
    [InlineData("Ábc١!x")]
    [InlineData("Abc1€x")]
    public void Accepts_boundary_longer_unicode_and_original_spaces(string password) => PasswordPolicy.Validate(password);

    [Fact]
    public void Does_not_add_a_maximum_length() => PasswordPolicy.Validate("Ab1!" + new string('x', 10000));
}
