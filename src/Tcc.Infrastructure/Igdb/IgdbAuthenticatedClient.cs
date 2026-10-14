using System.Text;
using System.Net;
using System.Net.Http.Headers;

namespace Tcc.Infrastructure.Igdb;

public sealed class IgdbAuthenticatedClient(HttpClient http, IgdbCredentialOptions options, TwitchTokenProvider tokens) : IDisposable
{
    public async Task<HttpResponseMessage> PostGamesAsync(string query, CancellationToken cancellationToken = default)
    {
        var token = await tokens.GetAsync(cancellationToken);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games");
                request.Headers.Add("Client-ID", options.ClientId);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
                request.Content = new StringContent(query, Encoding.UTF8, "text/plain");
                response = await http.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException)
            {
                throw new ExternalIntegrationUnavailableException();
            }
            if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
            response.Dispose();
            if (attempt == 0) token = await tokens.RefreshRejectedAsync(token, cancellationToken);
            else tokens.InvalidateRejected(token);
        }
        throw new ExternalIntegrationUnavailableException();
    }

    public void Dispose() => http.Dispose();
}
