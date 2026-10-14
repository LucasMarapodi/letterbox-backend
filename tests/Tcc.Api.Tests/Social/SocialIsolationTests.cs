using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;

namespace Tcc.Api.Tests.Social;

public sealed class SocialIsolationTests
{
    [Fact]
    public async Task New_host_has_no_relations_favorites_or_notifications_from_another_host()
    {
        await using var first = new TestApiFactory();
        await using var second = new TestApiFactory();
        using var client = first.CreateClient();
        using var other = second.CreateClient();
        var b = await SocialTest.Register(client, "B");
        await ApiTest.SignIn(client, "B@example.com", "p");
        await SocialTest.Follow(client, "user-1");
        await ApiTest.SignIn(client);
        using var favorite = await client.PutAsync("/api/v1/favorites/game/igdb-1", null);
        favorite.EnsureSuccessStatusCode();
        await ApiTest.Publish(client);
        Assert.Equal(b.Id, Assert.Single((await client.GetFromJsonAsync<PublicUser[]>("/api/v1/users/user-1/followers")) ?? []).Id);
        await ApiTest.SignIn(other);
        Assert.Empty((await other.GetFromJsonAsync<PublicUser[]>("/api/v1/users/user-1/followers")) ?? []);
        Assert.Empty((await other.GetFromJsonAsync<FavoriteLists>("/api/v1/favorites"))?.Games ?? []);
        Assert.Empty((await other.GetFromJsonAsync<NotificationDto[]>("/api/v1/notifications")) ?? []);
        Assert.Single((await client.GetFromJsonAsync<FavoriteLists>("/api/v1/favorites"))?.Games ?? []);
        await ApiTest.SignIn(client, "B@example.com", "p");
        Assert.Single((await client.GetFromJsonAsync<NotificationDto[]>("/api/v1/notifications")) ?? []);
    }
}
