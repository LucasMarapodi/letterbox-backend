using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Identity;

public sealed class ProfileTests
{
    [Fact]
    public async Task Public_profile_is_safe_and_missing_id_returns_404()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/users/user-1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<PublicUser>();
        Assert.Equal("Ana", user?.DisplayName);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "avatarUrl", "bio", "displayName", "id", "username" }, json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
        using var missing = await client.GetAsync("/api/v1/users/missing");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not_found", (await missing.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
    }

    [Fact]
    public async Task Patch_without_session_is_rejected()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await Patch(client, "{\"displayName\":\"Changed\"}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Partial_update_preserves_omitted_fields_clears_avatar_and_is_visible_in_me_and_public_read()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        var login = await LoginTests.Login(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        using var full = await Patch(client, "{\"displayName\":\"Ana Maria\",\"bio\":\"Nova bio\",\"avatarUrl\":\"https://example.com/avatar.png\"}");
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        using var name = await Patch(client, "{\"displayName\":\"  Ana Editada  \"}");
        var named = await name.Content.ReadFromJsonAsync<PublicUser>();
        Assert.Equal("Ana Editada", named?.DisplayName);
        Assert.Equal("Nova bio", named?.Bio);
        Assert.Equal("https://example.com/avatar.png", named?.AvatarUrl);
        using var clear = await Patch(client, "{\"avatarUrl\":null}");
        Assert.Null((await clear.Content.ReadFromJsonAsync<PublicUser>())?.AvatarUrl);
        using var noChange = await Patch(client, "{}");
        Assert.Equal(HttpStatusCode.OK, noChange.StatusCode);
        using var me = await client.GetAsync("/api/v1/auth/me");
        var current = await me.Content.ReadFromJsonAsync<PublicUser>();
        Assert.Equal("Ana Editada", current?.DisplayName);
        client.DefaultRequestHeaders.Authorization = null;
        var publicUser = await client.GetFromJsonAsync<PublicUser>($"/api/v1/users/{login.User.Id}");
        Assert.Equal(current, publicUser);
        Assert.Equal(login.User.Username, current?.Username);
    }

    [Theory]
    [InlineData("{\"displayName\":\" \"}")]
    [InlineData("{\"displayName\":null}")]
    [InlineData("{\"bio\":null}")]
    [InlineData("{\"avatarUrl\":\"ftp://example.com/a\"}")]
    [InlineData("{\"avatarUrl\":\"/relative.png\"}")]
    [InlineData("{\"avatarUrl\":\"\"}")]
    [InlineData("{\"username\":\"other\"}")]
    [InlineData("{\"email\":\"other@example.com\"}")]
    [InlineData("{\"password\":\"changed\"}")]
    [InlineData("{\"displayName\":\"Changed\",\"userId\":\"user-1\"}")]
    [InlineData("{\"displayName\":123}")]
    public async Task Invalid_patch_does_not_change_any_account(string json)
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "p"));
        registered.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, "b@example.com", "p");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        using var response = await Patch(client, json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", (await response.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        var own = await client.GetFromJsonAsync<PublicUser>($"/api/v1/users/{login.User.Id}");
        var ana = await client.GetFromJsonAsync<PublicUser>("/api/v1/users/user-1");
        Assert.Equal(login.User, own);
        Assert.Equal("Ana", ana?.DisplayName);
    }

    [Fact]
    public async Task Editing_current_profile_cannot_change_another_user_and_survives_new_requests()
    {
        await using var first = new WebApplicationFactory<Program>();
        await using var second = new WebApplicationFactory<Program>();
        using var client = first.CreateClient();
        using var otherHost = second.CreateClient();
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "p"));
        registration.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, "b@example.com", "p");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        using var edited = await Patch(client, "{\"displayName\":\"B edited\"}");
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("Ana", (await client.GetFromJsonAsync<PublicUser>("/api/v1/users/user-1"))?.DisplayName);
        Assert.Equal("B edited", (await client.GetFromJsonAsync<PublicUser>($"/api/v1/users/{login.User.Id}"))?.DisplayName);
        Assert.Equal("Ana", (await otherHost.GetFromJsonAsync<PublicUser>("/api/v1/users/user-1"))?.DisplayName);
        using var missing = await otherHost.GetAsync($"/api/v1/users/{login.User.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Text_limits_are_enforced_atomically()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        var login = await LoginTests.Login(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var valid = new ProfileInput(new string('n', 100), new string('b', 2000), "http://example.com/a");
        using var allowed = await Patch(client, JsonSerializer.Serialize(valid, JsonSerializerOptions.Web));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var baseline = await allowed.Content.ReadFromJsonAsync<PublicUser>();
        var invalid = new[]
        {
            valid with { DisplayName = new string('n', 101) },
            valid with { Bio = new string('b', 2001) },
            valid with { AvatarUrl = "https://example.com/" + new string('a', 2048) }
        };
        foreach (var input in invalid)
        {
            using var response = await Patch(client, JsonSerializer.Serialize(input, JsonSerializerOptions.Web));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(baseline, await client.GetFromJsonAsync<PublicUser>("/api/v1/users/user-1"));
    }

    private sealed record ProfileInput(string DisplayName, string Bio, string? AvatarUrl);

    internal static async Task<HttpResponseMessage> Patch(HttpClient client, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PatchAsync("/api/v1/users/me/profile", content);
    }
}
