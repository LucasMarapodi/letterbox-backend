namespace Tcc.Infrastructure.Igdb;

public sealed class TwitchTokenProvider(Func<CancellationToken, Task<TwitchAppToken>> acquire, TimeProvider clock)
{
    public Task<TwitchAppToken> GetAsync(CancellationToken cancellationToken = default)
    {
        _ = clock;
        return acquire(cancellationToken);
    }
    public Task<TwitchAppToken> RefreshRejectedAsync(TwitchAppToken rejected, CancellationToken cancellationToken = default)
    {
        _ = rejected;
        return acquire(cancellationToken);
    }
}
