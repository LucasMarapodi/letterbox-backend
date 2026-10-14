namespace Tcc.Infrastructure.Igdb;

public sealed class TwitchTokenProvider(Func<CancellationToken, Task<TwitchAppToken>> acquire, TimeProvider clock)
{
    private readonly object gate = new();
    private TwitchAppToken? current;
    private Task<TwitchAppToken>? inFlight;

    public Task<TwitchAppToken> GetAsync(CancellationToken cancellationToken = default) => GetSharedAsync(null, cancellationToken);

    public Task<TwitchAppToken> RefreshRejectedAsync(TwitchAppToken rejected, CancellationToken cancellationToken = default) =>
        GetSharedAsync(rejected, cancellationToken);

    public void InvalidateRejected(TwitchAppToken rejected)
    {
        lock (gate)
            if (ReferenceEquals(current, rejected)) current = null;
    }

    private Task<TwitchAppToken> GetSharedAsync(TwitchAppToken? rejected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource<TwitchAppToken>? owner = null;
        Task<TwitchAppToken> shared;
        lock (gate)
        {
            if (rejected is not null && ReferenceEquals(current, rejected)) current = null;
            if (current is not null && clock.GetUtcNow() < current.RefreshAt) return Task.FromResult(current);
            if (inFlight is null)
            {
                owner = new(TaskCreationOptions.RunContinuationsAsynchronously);
                inFlight = owner.Task;
            }
            shared = inFlight;
        }
        // Publish ownership before starting I/O, including sources that complete synchronously.
        if (owner is not null) _ = AcquireAndPublishAsync(owner);
        return shared.WaitAsync(cancellationToken);
    }

    private async Task AcquireAndPublishAsync(TaskCompletionSource<TwitchAppToken> completion)
    {
        try
        {
            // Individual callers cancel their wait; the shared acquisition has the HTTP timeout.
            var token = await acquire(CancellationToken.None);
            lock (gate)
            {
                current = token;
                inFlight = null;
            }
            completion.SetResult(token);
        }
        catch (Exception failure)
        {
            lock (gate) inFlight = null;
            completion.SetException(new ExternalIntegrationUnavailableException(
                failure is Tcc.Application.Catalog.CatalogIntegrationUnavailableException { AllowsStaleFallback: true }));
        }
    }
}
