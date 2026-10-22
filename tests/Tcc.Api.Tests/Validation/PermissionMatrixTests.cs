using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;

namespace Tcc.Api.Tests.Validation;

public sealed class PermissionMatrixTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("false")]
    [InlineData("expired")]
    [InlineData("logged-out")]
    public async Task Every_private_operation_rejects_missing_false_expired_or_revoked_session(string state)
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        if (state == "false") client.DefaultRequestHeaders.Authorization = new("Bearer", "false-token");
        if (state is "expired" or "logged-out")
        {
            await ApiTest.SignIn(client);
            if (state == "expired") clock.UtcNow = clock.UtcNow.AddMinutes(60);
            else { using var logout = await client.PostAsync("/api/v1/auth/logout", null); logout.EnsureSuccessStatusCode(); }
        }
        foreach (var operation in EndpointInventory.All.Where(operation => operation.Private))
        {
            var url = Resolve(operation.Path);
            var json = operation.Path.Contains("rating", StringComparison.Ordinal) && operation.Method == "put" ? "{\"value\":3}" :
                operation.Path == "/api/v1/users/me/profile" ? "{\"displayName\":\"Changed\"}" :
                operation.Method is "post" or "patch" && operation.Path.Contains("reviews", StringComparison.Ordinal) ? "{\"text\":\"Text\"}" : null;
            using var response = json is null
                ? await client.SendAsync(new HttpRequestMessage(new HttpMethod(operation.Method), url))
                : await ApiTest.Json(client, new HttpMethod(operation.Method), url, json);
            Assert.True((int)response.StatusCode == 401, $"{state}: {operation.Method} {operation.Path} => {response.StatusCode}");
            Assert.Equal("unauthorized", (await response.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        }
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal("Ana", (await client.GetFromJsonAsync<PublicUser>("/api/v1/users/1"))?.DisplayName);
        Assert.Empty((await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/games/1/reviews")) ?? []);
    }

    [Fact]
    public async Task Every_public_read_operation_works_without_token()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        foreach (var operation in EndpointInventory.All.Where(operation => !operation.Private && operation.Method == "get"))
        {
            using var response = await client.GetAsync(operation.Path == "/api/v1/search" ? "/api/v1/search?q=Atlas&scope=games" : Resolve(operation.Path));
            Assert.True((int)response.StatusCode == 200, operation.Path);
        }
    }

    private static string Resolve(string path) => path.Replace("{igdbId}", "1").Replace("{userId}", "1").Replace("{gameId}", "1")
        .Replace("{reviewId}", "1").Replace("{commentId}", "1").Replace("{kind}", "game").Replace("{targetId}", "1").Replace("{topicId}", "1");
}
