using System.Net;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class TokenProviderTests
{
    [Fact]
    public async Task Concurrent_readers_share_a_single_acquisition_and_the_same_token()
    {
        var clock = new TestClock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var http = new HttpClient(new TestHandler(async (_, ct) =>
        {
            Interlocked.Increment(ref calls);
            await release.Task.WaitAsync(ct);
            return IgdbTest.TokenResponse();
        }));
        var source = new TwitchOAuthClient(http, IgdbTest.Options(), clock);
        var provider = new TwitchTokenProvider(source.AcquireAsync, clock);
        var requests = Enumerable.Range(0, 24).Select(_ => provider.GetAsync()).ToArray();
        try { Assert.Equal(1, calls); }
        finally { release.SetResult(); }
        var tokens = await Task.WhenAll(requests);
        Assert.All(tokens, token => Assert.Same(tokens[0], token));
        Assert.Same(tokens[0], await provider.GetAsync());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("30", 90)]
    [InlineData("0", 120)]
    [InlineData("20", 100)]
    public async Task Renews_at_the_configured_boundary_and_not_before(string margin, int renewalSeconds)
    {
        var clock = new TestClock();
        var started = clock.Now;
        var calls = 0;
        using var http = new HttpClient(new TestHandler((_, _) => Task.FromResult(IgdbTest.TokenResponse($"token-{++calls}"))));
        var source = new TwitchOAuthClient(http, IgdbTest.Options(margin), clock);
        var provider = new TwitchTokenProvider(source.AcquireAsync, clock);
        var first = await provider.GetAsync();
        clock.Now = started.AddSeconds(renewalSeconds).AddTicks(-1);
        Assert.Same(first, await provider.GetAsync());
        clock.Now = started.AddSeconds(renewalSeconds);
        Assert.NotSame(first, await provider.GetAsync());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Short_lived_token_clamps_refresh_margin_to_half_the_lifetime()
    {
        var clock = new TestClock();
        var calls = 0;
        using var http = new HttpClient(new TestHandler((_, _) => { calls++; return Task.FromResult(IgdbTest.TokenResponse(lifetime: 10)); }));
        var source = new TwitchOAuthClient(http, IgdbTest.Options(), clock);
        var provider = new TwitchTokenProvider(source.AcquireAsync, clock);
        var first = await provider.GetAsync();
        Assert.Equal(clock.Now.AddSeconds(5), first.RefreshAt);
        Assert.Same(first, await provider.GetAsync());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Delayed_401_for_old_token_does_not_discard_a_new_generation()
    {
        var clock = new TestClock();
        var calls = 0;
        using var http = new HttpClient(new TestHandler((_, _) => Task.FromResult(IgdbTest.TokenResponse($"token-{++calls}"))));
        var source = new TwitchOAuthClient(http, IgdbTest.Options(), clock);
        var provider = new TwitchTokenProvider(source.AcquireAsync, clock);
        var old = await provider.GetAsync();
        var replacement = await provider.RefreshRejectedAsync(old);
        Assert.NotSame(old, replacement);
        Assert.Same(replacement, await provider.RefreshRejectedAsync(old));
        Assert.Same(replacement, await provider.GetAsync());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Cancelling_one_waiter_does_not_abort_acquisition_for_others()
    {
        var clock = new TestClock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var http = new HttpClient(new TestHandler(async (_, ct) =>
        {
            Interlocked.Increment(ref calls);
            await release.Task.WaitAsync(ct);
            return IgdbTest.TokenResponse();
        }));
        var source = new TwitchOAuthClient(http, IgdbTest.Options(), clock);
        var provider = new TwitchTokenProvider(source.AcquireAsync, clock);
        using var cancellation = new CancellationTokenSource();
        var abandoned = provider.GetAsync(cancellation.Token);
        var remaining = provider.GetAsync();
        cancellation.Cancel();
        try { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned); }
        finally { release.SetResult(); }
        Assert.Equal("synthetic-token", (await remaining).Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Concurrent_failure_is_shared_without_caching_a_failed_token_and_later_attempt_can_recover()
    {
        var clock = new TestClock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var failing = true;
        using var http = new HttpClient(new TestHandler(async (_, ct) =>
        {
            Interlocked.Increment(ref calls);
            await release.Task.WaitAsync(ct);
            return failing ? IgdbTest.Json("synthetic-secret", HttpStatusCode.ServiceUnavailable) : IgdbTest.TokenResponse();
        }));
        var source = new TwitchOAuthClient(http, IgdbTest.Options(), clock);
        var provider = new TwitchTokenProvider(source.AcquireAsync, clock);
        var attempts = Enumerable.Range(0, 12).Select(_ => provider.GetAsync()).ToArray();
        release.SetResult();
        foreach (var attempt in attempts) await Assert.ThrowsAsync<ExternalIntegrationUnavailableException>(() => attempt);
        Assert.Equal(1, calls);
        failing = false;
        Assert.Equal("synthetic-token", (await provider.GetAsync()).Value);
        Assert.Equal(2, calls);
    }
}
