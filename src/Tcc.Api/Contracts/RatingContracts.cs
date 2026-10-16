using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Tcc.Domain.Opinions;

namespace Tcc.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RatingRequest([Required] double? Value);
public sealed record RatingResponse(string Id, int UserId, string GameId, double Value)
{
    public static RatingResponse From(Rating rating) => new(rating.Id, rating.UserId, rating.GameId, rating.Value.Value);
}
public sealed record MyRatingResponse(RatingResponse? Rating);
