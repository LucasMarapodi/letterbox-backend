using System.Net;
using System.Net.Http.Headers;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class ResilienceTests
{
    [Fact]
    public async Task Rolling_rate_is_shared_and_never_exceeds_three_starts_in_a_second()
    {
        var scheduler = new AdvancingScheduler();
        var policy = new IgdbResiliencePolicy(new(), scheduler);
        var starts = new List<DateTimeOffset>();
        for (var i = 0; i < 9; i++)
        {
            using var budget = policy.CreateBudget();
            using var response = await policy.SendAsync(_ =>
            { starts.Add(scheduler.Clock.GetUtcNow()); return Task.FromResult(IgdbTest.Json("[]")); }, budget);
        }
        foreach (var start in starts) Assert.True(starts.Count(time => time > start.AddSeconds(-1) && time <= start) <= 3);
        Assert.True(scheduler.Delays.Count >= 2);
    }

    [Fact]
    public async Task Concurrency_is_limited_and_queue_overflow_is_rejected_without_transport()
    {
        var policy = new IgdbResiliencePolicy(new() { MaxConcurrent = 1, QueueLimit = 1 }, new AdvancingScheduler());
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var firstBudget = policy.CreateBudget();
        using var secondBudget = policy.CreateBudget();
        using var thirdBudget = policy.CreateBudget();
        async Task<HttpResponseMessage> Transport(CancellationToken ct)
        { Interlocked.Increment(ref calls); entered.TrySetResult(); await release.Task.WaitAsync(ct); return IgdbTest.Json("[]"); }
        var first = policy.SendAsync(Transport, firstBudget);
        await entered.Task;
        var second = policy.SendAsync(Transport, secondBudget);
        try
        {
            Assert.Equal(1, calls);
            await Assert.ThrowsAsync<ExternalIntegrationUnavailableException>(() => policy.SendAsync(Transport, thirdBudget));
        }
        finally { release.TrySetResult(); }
        using var a = await first;
        using var b = await second;
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Transient_retries_have_a_finite_budget(int status)
    {
        var scheduler = new AdvancingScheduler();
        var policy = new IgdbResiliencePolicy(new(), scheduler);
        using var budget = policy.CreateBudget();
        var calls = 0;
        using var response = await policy.SendAsync(_ =>
        { calls++; return Task.FromResult(IgdbTest.Json("[]", (HttpStatusCode)status)); }, budget);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(3, calls);
        Assert.True(scheduler.Delays.Count >= 2);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    public async Task Success_and_non_transient_failures_are_not_retried(int status)
    {
        var policy = new IgdbResiliencePolicy(new(), new AdvancingScheduler());
        using var budget = policy.CreateBudget();
        var calls = 0;
        using var response = await policy.SendAsync(_ =>
        { calls++; return Task.FromResult(IgdbTest.Json("[]", (HttpStatusCode)status)); }, budget);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RetryAfter_is_respected_and_shared_as_a_cooldown()
    {
        var scheduler = new AdvancingScheduler();
        var policy = new IgdbResiliencePolicy(new(), scheduler);
        using var budget = policy.CreateBudget();
        var started = scheduler.Clock.GetUtcNow();
        var calls = 0;
        using var response = await policy.SendAsync(_ =>
        {
            if (++calls > 1) return Task.FromResult(IgdbTest.Json("[]"));
            var limited = IgdbTest.Json("[]", HttpStatusCode.TooManyRequests);
            limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
            return Task.FromResult(limited);
        }, budget);
        Assert.Equal(2, calls);
        Assert.True(scheduler.Clock.GetUtcNow() >= started.AddSeconds(3));
    }

    [Fact]
    public async Task RetryAfter_beyond_the_total_budget_ends_as_timeout_without_an_extra_request()
    {
        var scheduler = new AdvancingScheduler();
        var policy = new IgdbResiliencePolicy(new() { TotalTimeout = TimeSpan.FromSeconds(2) }, scheduler);
        using var budget = policy.CreateBudget();
        var calls = 0;
        await Assert.ThrowsAsync<CatalogTimeoutException>(() => policy.SendAsync(_ =>
        {
            calls++;
            var response = IgdbTest.Json("[]", HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            return Task.FromResult(response);
        }, budget));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Timeout_cancels_transport_and_does_not_expose_supplier_exception()
    {
        var policy = new IgdbResiliencePolicy(new() { TotalTimeout = TimeSpan.FromMilliseconds(30) }, new SystemIgdbScheduler(TimeProvider.System));
        using var budget = policy.CreateBudget();
        var failure = await Assert.ThrowsAsync<CatalogTimeoutException>(() => policy.SendAsync(async ct =>
        { await Task.Delay(200, ct); return IgdbTest.Json("[]"); }, budget));
        Assert.Null(failure.InnerException);
    }

    [Fact]
    public async Task Shared_retry_budget_is_not_reset_by_a_second_authenticated_send()
    {
        var policy = new IgdbResiliencePolicy(new(), new AdvancingScheduler());
        using var budget = policy.CreateBudget();
        var statuses = new Queue<HttpStatusCode>([HttpStatusCode.InternalServerError, HttpStatusCode.Unauthorized,
            HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError]);
        Task<HttpResponseMessage> Send(CancellationToken _) => Task.FromResult(IgdbTest.Json("[]", statuses.Dequeue()));
        using var first = await policy.SendAsync(Send, budget);
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        using var second = await policy.SendAsync(Send, budget);
        Assert.Equal(HttpStatusCode.InternalServerError, second.StatusCode);
        Assert.Empty(statuses);
    }

    [Fact]
    public async Task Authenticated_client_shares_retry_budget_across_401_recovery()
    {
        var scheduler = new AdvancingScheduler();
        using var policy = new IgdbResiliencePolicy(new(), scheduler);
        var oauthCalls = 0;
        using var oauth = new HttpClient(new TestHandler((_, _) => Task.FromResult(IgdbTest.TokenResponse($"token-{++oauthCalls}"))));
        var source = new TwitchOAuthClient(oauth, IgdbTest.Options(), scheduler.Clock);
        var statuses = new Queue<HttpStatusCode>([HttpStatusCode.InternalServerError, HttpStatusCode.Unauthorized,
            HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError]);
        using var http = new HttpClient(new TestHandler((_, _) => Task.FromResult(IgdbTest.Json("[]", statuses.Dequeue()))));
        using var client = new IgdbAuthenticatedClient(http, IgdbTest.Options(), new TwitchTokenProvider(source.AcquireAsync, scheduler.Clock), policy);
        using var response = await client.PostGamesAsync("fields id;");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(2, oauthCalls);
        Assert.Empty(statuses);
    }
}

internal sealed class AdvancingScheduler : IIgdbScheduler
{
    private readonly TestClock clock = new();
    public TimeProvider Clock => clock;
    public List<TimeSpan> Delays { get; } = [];
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); Delays.Add(delay); clock.Now += delay; return Task.CompletedTask; }
}
