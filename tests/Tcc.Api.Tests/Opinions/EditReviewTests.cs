using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcc.Api.Tests.Identity;

namespace Tcc.Api.Tests.Opinions;

public sealed class EditReviewTests
{
    [Fact]
    public async Task Author_edit_preserves_identity_and_publication_and_changes_only_text_and_update_date()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var original = await ApiTest.Publish(client);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        using var response = await client.PatchAsJsonAsync($"/api/v1/reviews/{original.Id}", new ReviewInput("  Edited  "));
        Assert.Equal(200, (int)response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ReviewDto>();
        Assert.NotNull(updated);
        Assert.Equal(original with { Text = "Edited", UpdatedAt = clock.GetUtcNow() }, updated);
        Assert.Equal(updated, await client.GetFromJsonAsync<ReviewDto>($"/api/v1/reviews/{original.Id}"));
        Assert.Single((await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/games/1/reviews")) ?? []);
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("B", "b@example.com", "p"));
        registration.EnsureSuccessStatusCode();
        await ApiTest.SignIn(client, "b@example.com", "p");
        using var forbidden = await client.PatchAsJsonAsync($"/api/v1/reviews/{original.Id}", new ReviewInput("Other"));
        Assert.Equal(403, (int)forbidden.StatusCode);
        Assert.Equal(updated, await client.GetFromJsonAsync<ReviewDto>($"/api/v1/reviews/{original.Id}"));
        client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await client.PatchAsJsonAsync($"/api/v1/reviews/{original.Id}", new ReviewInput("Other"));
        Assert.Equal(401, (int)anonymous.StatusCode);
    }

    [Theory]
    [InlineData("{\"rating\":null}")]
    [InlineData("{\"text\":\"New\",\"gameId\":\"igdb-2\"}")]
    [InlineData("{\"text\":\"New\",\"userId\":\"other\"}")]
    [InlineData("{\"text\":\"New\",\"createdAt\":\"2020-01-01T00:00:00Z\"}")]
    public async Task Invalid_edits_are_atomic(string json)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var original = await ApiTest.Publish(client);
        using var response = await ApiTest.Json(client, HttpMethod.Patch, $"/api/v1/reviews/{original.Id}", json);
        Assert.Equal(400, (int)response.StatusCode);
        Assert.Equal(original, await client.GetFromJsonAsync<ReviewDto>($"/api/v1/reviews/{original.Id}"));
    }

    [Fact]
    public async Task Missing_review_returns_404()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        using var response = await client.PatchAsJsonAsync("/api/v1/reviews/999999", new ReviewInput("New"));
        Assert.Equal(404, (int)response.StatusCode);
    }
}
