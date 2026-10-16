using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;

namespace Tcc.Api.Tests.Social;

public sealed class FollowTests
{
    [Fact]
    public async Task Relations_use_session_direction_and_are_idempotent_under_concurrency()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        var b = await SocialTest.Register(client, "B");
        var c = await SocialTest.Register(client, "C");
        await ApiTest.SignIn(client);
        var results = await Task.WhenAll(client.PutAsync($"/api/v1/users/{b.Id}/follow", null), client.PutAsync($"/api/v1/users/{b.Id}/follow", null));
        try { Assert.All(results, response => Assert.Equal(200, (int)response.StatusCode)); }
        finally { foreach (var response in results) response.Dispose(); }
        Assert.True((await client.GetFromJsonAsync<FollowStatus>($"/api/v1/users/{b.Id}/follow/me"))?.Following);
        await ApiTest.SignIn(client, "B@example.com", "p");
        await SocialTest.Follow(client, c.Id);
        Assert.False((await client.GetFromJsonAsync<FollowStatus>("/api/v1/users/user-1/follow/me"))?.Following);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal("user-1", Assert.Single((await client.GetFromJsonAsync<PublicUser[]>($"/api/v1/users/{b.Id}/followers")) ?? []).Id);
        Assert.Equal(c.Id, Assert.Single((await client.GetFromJsonAsync<PublicUser[]>($"/api/v1/users/{b.Id}/following")) ?? []).Id);
        await ApiTest.SignIn(client);
        for (var i = 0; i < 2; i++)
        {
            using var deleted = await client.DeleteAsync($"/api/v1/users/{b.Id}/follow");
            Assert.Equal(204, (int)deleted.StatusCode);
        }
        Assert.Empty((await client.GetFromJsonAsync<PublicUser[]>($"/api/v1/users/{b.Id}/followers")) ?? []);
    }

    [Fact]
    public async Task Self_missing_anonymous_and_body_forgery_are_rejected()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var anonymous = await client.PutAsync("/api/v1/users/user-1/follow", null);
        Assert.Equal(401, (int)anonymous.StatusCode);
        using var status = await client.GetAsync("/api/v1/users/user-1/follow/me");
        Assert.Equal(401, (int)status.StatusCode);
        var b = await SocialTest.Register(client, "B");
        await ApiTest.SignIn(client);
        using var self = await client.PutAsync("/api/v1/users/user-1/follow", null);
        Assert.Equal(400, (int)self.StatusCode);
        using var missing = await client.PutAsync("/api/v1/users/missing/follow", null);
        Assert.Equal(404, (int)missing.StatusCode);
        using var forged = await ApiTest.Json(client, HttpMethod.Put, $"/api/v1/users/{b.Id}/follow", "{\"userId\":\"other\"}");
        Assert.Equal(400, (int)forged.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<PublicUser[]>($"/api/v1/users/{b.Id}/followers")) ?? []);
    }
}
