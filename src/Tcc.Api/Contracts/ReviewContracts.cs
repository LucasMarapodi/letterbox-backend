using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Tcc.Application.Opinions;

namespace Tcc.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TextRequest([Required] string? Text);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewCreateRequest([Required] [property: JsonNumberHandling(JsonNumberHandling.Strict)] double? Rating, string? Text = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReviewPatchRequest
{
    private string? text;
    private double? rating;
    public string? Text { get => text; set { text = value; TextSpecified = true; } }
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public double? Rating { get => rating; set { rating = value; RatingSpecified = true; } }
    [JsonIgnore] public bool TextSpecified { get; private set; }
    [JsonIgnore] public bool RatingSpecified { get; private set; }
}
public sealed record AuthorResponse(int Id, string DisplayName, string? AvatarUrl);
public sealed record GameSummaryResponse(int Id, string Title, string? CoverUrl);
public sealed record MyReviewResponse(ReviewResponse? Review);
public sealed record GameOpinionsResponse(double? AverageRating, IReadOnlyList<ReviewResponse> Reviews);
public sealed record ReviewResponse(int Id, int UserId, int GameId, string Text,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, AuthorResponse Author, GameSummaryResponse Game, double? Rating, int LikeCount)
{
    public static ReviewResponse From(ReviewDetails details) => new(details.Review.Id, details.Review.UserId,
        details.Review.GameId, details.Review.Text, details.Review.CreatedAt, details.Review.UpdatedAt,
        new(details.Author.Id, details.Author.DisplayName, details.Author.AvatarUrl),
        new(details.Game.Id, details.Game.Title, details.Game.CoverUrl), details.Review.Evaluation?.Value.Value, details.LikeCount);
}
