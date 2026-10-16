using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tcc.Application.Identity;
using Tcc.Domain.Identity;

namespace Tcc.Api.Tests.Identity;

public sealed class StorageTests
{
    [Fact]
    public async Task Stored_passwords_are_salted_verifiers_and_plaintext_is_never_stored()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var first = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("A", "a@example.com", "p"));
        using var second = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "p"));
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var accounts = host.Services.GetRequiredService<IUserAccounts>();
        var passwords = host.Services.GetRequiredService<IPasswordHasher>();
        var a = accounts.FindByEmail(Email.Create("a@example.com"));
        var b = accounts.FindByEmail(Email.Create("b@example.com"));
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotEqual("p", a.PasswordHash);
        Assert.NotEqual(a.PasswordHash, b.PasswordHash);
        Assert.True(passwords.Verify("p", a.PasswordHash));
        Assert.False(passwords.Verify("wrong", a.PasswordHash));
        Assert.False(passwords.Verify("p", "malformed-verifier"));
        Assert.False(passwords.Verify("p", "pbkdf2-sha512:210000:invalid:invalid"));
        using var meWithoutLogin = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meWithoutLogin.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Invalid_session_duration_fails_host_startup(string minutes)
    {
        using var host = new IdentityFactory(new MutableClock(DateTimeOffset.UtcNow), minutes);
        Assert.Throws<OptionsValidationException>(() => host.CreateClient());
    }

    [Fact]
    public async Task Concurrent_partial_edits_do_not_lose_independent_fields()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        var login = await LoginTests.Login(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var responses = await Task.WhenAll(ProfileTests.Patch(client, "{\"displayName\":\"Concurrent\"}"),
            ProfileTests.Patch(client, "{\"bio\":\"Concurrent bio\"}"));
        try { foreach (var response in responses) Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
        finally { foreach (var response in responses) response.Dispose(); }
        var user = await client.GetFromJsonAsync<PublicUser>("/api/v1/users/user-1");
        Assert.Equal("Concurrent", user?.DisplayName);
        Assert.Equal("Concurrent bio", user?.Bio);
    }
}
