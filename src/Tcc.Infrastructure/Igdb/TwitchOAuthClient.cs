namespace Tcc.Infrastructure.Igdb;

public sealed class TwitchOAuthClient(HttpClient http, IgdbCredentialOptions options, TimeProvider clock)
{
    public Task<TwitchAppToken> AcquireAsync(CancellationToken cancellationToken = default)
    {
        _ = http; _ = options; _ = clock; _ = cancellationToken;
        throw new NotImplementedException();
    }
}
