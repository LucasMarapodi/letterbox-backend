using Tcc.Application.Catalog;
using System.Net;

namespace Tcc.Infrastructure.Igdb;

public sealed record IgdbResilienceOptions
{
    public int RequestsPerSecond { get; init; } = 3;
    public int MaxConcurrent { get; init; } = 4;
    public int QueueLimit { get; init; } = 32;
    public int MaxRetries { get; init; } = 2;
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public void Validate()
    {
        if (RequestsPerSecond is < 1 or > 4 || MaxConcurrent is < 1 or > 8 || QueueLimit is < 0 or > 128
            || MaxRetries is < 0 or > 2 || TotalTimeout <= TimeSpan.Zero || TotalTimeout > TimeSpan.FromMinutes(1))
            throw new InvalidOperationException("Configuração de resiliência IGDB inválida.");
    }
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
public sealed class IgdbCallBudget : IDisposable
{
    private readonly CancellationTokenSource timeout;
    private readonly CancellationTokenSource linked;
    private readonly IIgdbScheduler scheduler;
    private int retries;
    public CancellationToken Caller { get; }
    public DateTimeOffset Deadline { get; }
    public CancellationToken Token => linked.Token;
    internal int RetriesUsed { get; private set; }
    internal IgdbCallBudget(IgdbResilienceOptions options, IIgdbScheduler scheduler, CancellationToken caller)
    {
        this.scheduler = scheduler; Caller = caller; retries = options.MaxRetries;
        Deadline = scheduler.Clock.GetUtcNow() + options.TotalTimeout;
        timeout = new(options.TotalTimeout, scheduler.Clock);
        linked = CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token);
    }
    internal bool Retry() { if (Interlocked.Decrement(ref retries) < 0) return false; RetriesUsed++; return true; }
    internal void Check()
    {
        Caller.ThrowIfCancellationRequested();
        if (timeout.IsCancellationRequested || scheduler.Clock.GetUtcNow() >= Deadline) throw new CatalogTimeoutException();
    }
    public void Dispose() { linked.Dispose(); timeout.Dispose(); }
}
public sealed class IgdbResiliencePolicy : IDisposable
{
    private readonly IgdbResilienceOptions options;
    private readonly IIgdbScheduler scheduler;
    private readonly SemaphoreSlim slots;
    private readonly SemaphoreSlim rate = new(1, 1);
    private readonly Queue<DateTimeOffset> starts = new();
    private readonly object gate = new();
    private int admitted;
    private DateTimeOffset cooldown;
    public IgdbResiliencePolicy(IgdbResilienceOptions options, IIgdbScheduler scheduler)
    {
        options.Validate();
        this.options = options; this.scheduler = scheduler; slots = new(options.MaxConcurrent, options.MaxConcurrent);
    }
    public IgdbCallBudget CreateBudget(CancellationToken caller = default) => new(options, scheduler, caller);
    public async Task<HttpResponseMessage> SendAsync(Func<CancellationToken, Task<HttpResponseMessage>> send, IgdbCallBudget budget)
    {
        try
        {
            while (true)
            {
                budget.Check();
                HttpResponseMessage response;
                await EnterAsync(budget);
                try { response = await send(budget.Token); }
                finally { slots.Release(); lock (gate) admitted--; }
                try { budget.Check(); }
                catch { response.Dispose(); throw; }
                var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                if (!transient) return response;
                var delay = RetryDelay(response, budget.RetriesUsed);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    lock (gate) cooldown = Max(cooldown, scheduler.Clock.GetUtcNow() + delay);
                if (!budget.Retry()) return response;
                response.Dispose();
                if (delay >= budget.Deadline - scheduler.Clock.GetUtcNow()) throw new CatalogTimeoutException();
                await scheduler.DelayAsync(delay, budget.Token);
            }
        }
        catch (OperationCanceledException)
        {
            if (budget.Caller.IsCancellationRequested) throw new OperationCanceledException(budget.Caller);
            throw new CatalogTimeoutException();
        }
    }
    private async Task EnterAsync(IgdbCallBudget budget)
    {
        lock (gate)
        {
            if (admitted >= options.MaxConcurrent + options.QueueLimit) throw new ExternalIntegrationUnavailableException();
            admitted++;
        }
        var ownsSlot = false;
        try
        {
            await slots.WaitAsync(budget.Token); ownsSlot = true;
            await rate.WaitAsync(budget.Token);
            try
            {
                while (true)
                {
                    budget.Check();
                    var now = scheduler.Clock.GetUtcNow();
                    while (starts.TryPeek(out var oldest) && oldest <= now.AddSeconds(-1)) starts.Dequeue();
                    DateTimeOffset next;
                    lock (gate) next = cooldown;
                    if (starts.Count >= options.RequestsPerSecond) next = Max(next, starts.Peek().AddSeconds(1));
                    if (next <= now) { starts.Enqueue(now); break; }
                    if (next >= budget.Deadline) throw new CatalogTimeoutException();
                    await scheduler.DelayAsync(next - now, budget.Token);
                }
            }
            finally { rate.Release(); }
        }
        catch
        {
            if (ownsSlot) slots.Release();
            lock (gate) admitted--;
            throw;
        }
    }
    private TimeSpan RetryDelay(HttpResponseMessage response, int retry)
    {
        var after = response.Headers.RetryAfter;
        var value = after?.Delta ?? (after?.Date is DateTimeOffset date ? date - scheduler.Clock.GetUtcNow() : null);
        if (value is TimeSpan provided) return provided > TimeSpan.Zero ? provided : TimeSpan.Zero;
        return TimeSpan.FromMilliseconds(200 * Math.Pow(2, retry) + Random.Shared.Next(0, 50));
    }
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
    public void Dispose() { slots.Dispose(); rate.Dispose(); }
}
