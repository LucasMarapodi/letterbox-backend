using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Tcc.Api.Tests.Identity;

public sealed class PasswordPolicyHttpTests
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
    [InlineData("Abc1\u0001x")]
    public async Task Invalid_password_returns_safe_400_and_does_not_reserve_email(string? password)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var invalid = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("User", "policy@example.com", password));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var error = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "code", "message" }, error.RootElement.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("validation_failed", error.RootElement.GetProperty("code").GetString());
        Assert.Equal("Requisição inválida.", error.RootElement.GetProperty("message").GetString());
        using var valid = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("User", "policy@example.com", "Abc1!x"));
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
    }

    [Theory]
    [InlineData("Abc1!x")]
    [InlineData("ABC1!X")]
    [InlineData(" Ab1! ")]
    [InlineData(" Abc1!longer ")]
    public async Task Valid_registration_keeps_public_contract_and_exact_password(string password)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("User", "policy@example.com", password));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "avatarUrl", "bannerPositionX", "bannerPositionY", "bannerUrl", "bio", "displayName", "id", "username" }, json.RootElement.EnumerateObject().Select(property => property.Name).Order());
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginInput("policy@example.com", password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var changed = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginInput("policy@example.com", password + "x"));
        Assert.Equal(HttpStatusCode.Unauthorized, changed.StatusCode);
        if (password != password.Trim())
        {
            using var trimmed = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginInput("policy@example.com", password.Trim()));
            Assert.Equal(HttpStatusCode.Unauthorized, trimmed.StatusCode);
        }
    }

    [Fact]
    public async Task Preexisting_account_with_legacy_password_still_authenticates()
    {
        // Test host seeds this account directly with a real PBKDF2 verifier, bypassing registration.
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginInput("ana@example.com", "teste123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("ana", (await login.Content.ReadFromJsonAsync<LoginResult>())?.User.Username);
    }
}
