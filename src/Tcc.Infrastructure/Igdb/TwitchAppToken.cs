using System.Text.Json.Serialization;

namespace Tcc.Infrastructure.Igdb;

public sealed class TwitchAppToken(string value, DateTimeOffset expiresAt, DateTimeOffset refreshAt)
{
    [JsonIgnore] public string Value { get; } = value;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public DateTimeOffset RefreshAt { get; } = refreshAt;
    public override string ToString() => "Twitch application token (redacted)";
}
