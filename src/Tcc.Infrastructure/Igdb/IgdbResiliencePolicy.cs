namespace Tcc.Infrastructure.Igdb;

public sealed record IgdbResilienceOptions
{
    public int RequestsPerSecond { get; init; } = 3;
    public int MaxConcurrent { get; init; } = 4;
    public int QueueLimit { get; init; } = 32;
    public int MaxRetries { get; init; } = 2;
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
public interface IIgdbScheduler
{
    TimeProvider Clock { get; }
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
public sealed class SystemIgdbScheduler(TimeProvider clock) : IIgdbScheduler
{
    public TimeProvider Clock { get; } = clock;
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, Clock, cancellationToken);
}
public sealed class IgdbCallBudget(CancellationToken caller) : IDisposable
{
    public CancellationToken Token { get; } = caller;
    public void Dispose() { }
}
public sealed class IgdbResiliencePolicy(IgdbResilienceOptions options, IIgdbScheduler scheduler)
{
    public IgdbCallBudget CreateBudget(CancellationToken caller = default) { _ = options; _ = scheduler; return new(caller); }
    public Task<HttpResponseMessage> SendAsync(Func<CancellationToken, Task<HttpResponseMessage>> send, IgdbCallBudget budget) => send(budget.Token);
}
