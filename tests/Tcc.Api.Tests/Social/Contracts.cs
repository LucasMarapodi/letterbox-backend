using System.Net.Http.Json;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;

namespace Tcc.Api.Tests.Social;

public sealed record FollowStatus(bool Following);
public sealed record FavoriteDto(string Id, int UserId, string TargetType, string TargetId);
public sealed record FavoriteStatus(FavoriteDto? Favorite);
public sealed record FavoriteLists(GameDto[] Games, ReviewDto[] Reviews);
public sealed record NotificationDto(string Id, int UserId, int ActorId, int ReviewId,
    DateTimeOffset CreatedAt, string Type, string? CommentId, AuthorDto Actor);

internal static class SocialTest
{
    public static async Task<PublicUser> Register(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput(name, $"{name}@example.com", "p"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PublicUser>() ?? throw new InvalidOperationException("Usuário ausente.");
    }
    public static async Task Follow(HttpClient client, int target)
    {
        using var response = await client.PutAsync($"/api/v1/users/{target}/follow", null);
        response.EnsureSuccessStatusCode();
    }
}
