using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Identity;

public sealed class RegistrationTests
{
    [Fact]
    public async Task Register_returns_public_user_without_starting_session()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("  Lucas  ", " Lucas@example.com ", "p"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<PublicUser>();
        Assert.NotNull(user);
        Assert.False(string.IsNullOrWhiteSpace(user.Id));
        Assert.Equal($"user-{user.Id}", user.Username);
        Assert.Equal("Lucas", user.DisplayName);
        Assert.Equal("", user.Bio);
        Assert.Null(user.AvatarUrl);
        Assert.Equal($"/api/v1/users/{user.Id}", response.Headers.Location?.OriginalString);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "avatarUrl", "bio", "displayName", "id", "username" }, json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Null(response.Headers.WwwAuthenticate.FirstOrDefault());
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Email_uniqueness_is_normalized_and_atomic_under_concurrent_requests()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("One", " Test@example.com ", "p")),
            client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Two", "test@EXAMPLE.COM", "p")));
        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            var duplicate = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("conflict", (await duplicate.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData(null, "a@example.com", "p")]
    [InlineData(" ", "a@example.com", "p")]
    [InlineData("A", null, "p")]
    [InlineData("A", "not-an-email", "p")]
    [InlineData("A", "Name <a@example.com>", "p")]
    [InlineData("A", "a@example.com", null)]
    [InlineData("A", "a@example.com", " ")]
    public async Task Invalid_fields_return_standard_400(string? name, string? email, string? password)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput(name, email, password));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", (await response.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
    }

    [Fact]
    public async Task Accounts_are_isolated_and_seed_does_not_reset_registered_accounts()
    {
        await using var first = new WebApplicationFactory<Program>();
        await using var second = new WebApplicationFactory<Program>();
        using var a = first.CreateClient();
        using var b = second.CreateClient();
        var input = new RegisterInput("A", "a@example.com", "p");
        using var created = await a.PostAsJsonAsync("/api/v1/auth/register", input);
        using var duplicate = await a.PostAsJsonAsync("/api/v1/auth/register", input);
        using var isolated = await b.PostAsJsonAsync("/api/v1/auth/register", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, isolated.StatusCode);
    }
}
