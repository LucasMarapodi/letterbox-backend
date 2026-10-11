using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tcc.Api.Tests.Identity;

public sealed class LoginTests
{
    [Fact]
    public async Task Registered_account_can_login_with_normalized_email_and_exact_password()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Lucas", "lucas@example.com", " p "));
        registration.EnsureSuccessStatusCode();
        var account = await registration.Content.ReadFromJsonAsync<PublicUser>();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginInput(" LUCAS@EXAMPLE.COM ", " p "));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(login);
        Assert.Equal(account?.Id, login.User.Id);
        Assert.False(string.IsNullOrWhiteSpace(login.Token));
        Assert.Equal(TimeSpan.Zero, login.ExpiresAt.Offset);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "expiresAt", "token", "user" }, json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal(new[] { "avatarUrl", "bio", "displayName", "id", "username" }, json.RootElement.GetProperty("user").EnumerateObject().Select(property => property.Name).Order().ToArray());
        using var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginInput("lucas@example.com", "p"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    [Theory]
    [InlineData("ana@example.com", "wrong")]
    [InlineData("missing@example.com", "teste123")]
    [InlineData("not-an-email", "teste123")]
    public async Task Wrong_credentials_return_the_same_safe_401(string email, string password)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginInput(email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", (await response.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
    }

    [Fact]
    public async Task Each_login_has_its_own_token_and_configurable_expiration()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock, "15");
        using var client = host.CreateClient();
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "p"));
        registration.EnsureSuccessStatusCode();
        var first = await Login(client, "ana@example.com", "teste123");
        var sameAccount = await Login(client, "ana@example.com", "teste123");
        var second = await Login(client, "b@example.com", "p");
        Assert.NotEqual(first.Token, sameAccount.Token);
        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.User.Id, second.User.Id);
        Assert.Equal(clock.GetUtcNow().AddMinutes(15), first.ExpiresAt);
    }

    internal static async Task<LoginResult> Login(HttpClient client, string email = "ana@example.com", string password = "teste123")
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginInput(email, password));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(login);
        return login;
    }
}

public sealed class IdentityFactory(MutableClock clock, string minutes = "60") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Authentication:SessionMinutes"] = minutes }));
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
    }
}

public sealed class MutableClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
