using System.Text.Json.Serialization;

namespace Tcc.Infrastructure.Igdb;

public sealed class IgdbGameDto
{
    [JsonPropertyName("id")] public long? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    [JsonPropertyName("cover")] public IgdbCoverDto? Cover { get; init; }
    [JsonPropertyName("genres")] public IgdbNameDto[]? Genres { get; init; }
    [JsonPropertyName("platforms")] public IgdbNameDto[]? Platforms { get; init; }
    [JsonPropertyName("first_release_date")] public long? FirstReleaseDate { get; init; }
}
public sealed class IgdbCoverDto
{
    [JsonPropertyName("image_id")] public string? ImageId { get; init; }
}
public sealed class IgdbNameDto
{
    [JsonPropertyName("name")] public string? Name { get; init; }
}
