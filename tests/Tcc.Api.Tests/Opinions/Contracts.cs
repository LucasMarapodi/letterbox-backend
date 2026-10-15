using System.Net.Http.Json;
using System.Text;
using Tcc.Api.Tests.Identity;

namespace Tcc.Api.Tests.Opinions;

public sealed record GameDto(int Id, string Title, string Summary, string? CoverUrl, string[] Genres, string[] Platforms, DateOnly? ReleaseDate, double? AverageRating);
public sealed record RatingDto(string Id, int UserId, int GameId, double Value);
public sealed record MyRating(RatingDto? Rating);
public sealed record AuthorDto(int Id, string DisplayName, string? AvatarUrl);
public sealed record GameSummaryDto(int Id, string Title, string? CoverUrl);
public sealed record ReviewDto(string Id, int UserId, int GameId, string Text, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, AuthorDto Author, GameSummaryDto Game);
public sealed record MyReview(ReviewDto? Review);
public sealed record OpinionsDto(double? AverageRating, ReviewDto[] Reviews);
public sealed record CommentDto(string Id, int UserId, string ReviewId, string Text, DateTimeOffset CreatedAt, AuthorDto Author);
public sealed record TextInput(string? Text);
public sealed record RatingInput(double Value);

internal static class ApiTest
{
    public static async Task SignIn(HttpClient client, string email = "ana@example.com", string password = "teste123")
    {
        client.DefaultRequestHeaders.Authorization = null;
        var login = await LoginTests.Login(client, email, password);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
    }

    public static async Task<HttpResponseMessage> Json(HttpClient client, HttpMethod method, string url, string json)
    {
        using var request = new HttpRequestMessage(method, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        return await client.SendAsync(request);
    }

    public static async Task<ReviewDto> Publish(HttpClient client, int game = 1, string text = "Review")
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/games/{game}/reviews", new TextInput(text));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReviewDto>() ?? throw new InvalidOperationException("Review ausente.");
    }
}
