using System.Text;

namespace Tcc.Infrastructure.Igdb;

public sealed class IgdbAuthenticatedClient(HttpClient http, IgdbCredentialOptions options, TwitchTokenProvider tokens)
{
    public Task<HttpResponseMessage> PostGamesAsync(string query, CancellationToken cancellationToken = default)
    {
        _ = options; _ = tokens;
        return http.PostAsync("https://api.igdb.com/v4/games", new StringContent(query, Encoding.UTF8, "text/plain"), cancellationToken);
    }
}
