using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tcc.Infrastructure.Igdb;

public sealed class TwitchOAuthClient(HttpClient http, IgdbCredentialOptions options, TimeProvider clock,
    ExternalEndpointOptions? endpoints = null)
{
    private readonly Uri tokenEndpoint = (endpoints ?? ExternalEndpointOptions.Default).TokenEndpoint;
    public async Task<TwitchAppToken> AcquireAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = clock.GetUtcNow();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["grant_type"] = "client_credentials"
            });
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK) throw new ExternalIntegrationUnavailableException(
                response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500);
            var payload = JsonSerializer.Deserialize<OAuthTokenResponse>(await response.Content.ReadAsStringAsync(cancellationToken));
            if (payload is null || string.IsNullOrEmpty(payload.AccessToken) || payload.AccessToken.Length > 4096
                || payload.AccessToken.Any(character => character < '!' || character > '~')
                || payload.ExpiresIn is null or <= 0
                || !string.Equals(payload.TokenType, "bearer", StringComparison.OrdinalIgnoreCase))
                throw new ExternalIntegrationUnavailableException(false);
            var lifetime = TimeSpan.FromSeconds(payload.ExpiresIn.Value);
            var expiresAt = startedAt.Add(lifetime);
            if (clock.GetUtcNow() >= expiresAt) throw new ExternalIntegrationUnavailableException(false);
            var margin = TimeSpan.FromTicks(Math.Min(options.RefreshMargin.Ticks, lifetime.Ticks / 2));
            return new(payload.AccessToken, expiresAt, expiresAt.Subtract(margin));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or OperationCanceledException)
        {
            // The original exception can contain supplier payloads or credentials.
            throw new ExternalIntegrationUnavailableException(failure is not JsonException);
        }
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
        [JsonPropertyName("expires_in")] public int? ExpiresIn { get; init; }
        [JsonPropertyName("token_type")] public string? TokenType { get; init; }
    }
}
