using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tcc.Api.Configuration;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class SmokeScenarioTests
{
    [Theory]
    [InlineData(false, false, "DISABLED")]
    [InlineData(false, true, "DISABLED")]
    [InlineData(true, false, "BLOCKED")]
    public async Task Smoke_requires_opt_in_and_credentials_without_starting_host(bool requested, bool ready, string result)
    {
        var counters = new SmokeCounters();
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(_ => throw new InvalidOperationException("Host must not start")));
        var report = await LiveSmokeScenario.RunAsync(host, counters, requested, ready, "simulated");
        Assert.Equal(result, report.Result);
        Assert.Equal(0, report.OAuthRequests);
        Assert.Equal(0, report.IgdbRequests);
        Assert.Empty(report.Steps);
    }

    [Fact]
    public async Task Harness_exercises_public_API_token_cache_local_eviction_pages_and_search_with_bounded_transport()
    {
        var counters = new SmokeCounters();
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(IgdbTest.Options());
                services.AddSingleton(new IgdbResilienceOptions { MaxRetries = 0 });
                services.AddHttpClient(IgdbAuthenticationRegistration.OAuthClientName).ConfigurePrimaryHttpMessageHandler(() =>
                    new TestHandler((_, _) => { counters.Record(true); return Task.FromResult(IgdbTest.TokenResponse()); }));
                services.AddHttpClient(IgdbAuthenticationRegistration.IgdbClientName).ConfigurePrimaryHttpMessageHandler(() =>
                    new TestHandler((_, _) =>
                    {
                        counters.Record(false);
                        return Task.FromResult(IgdbTest.Json("[{\"id\":1,\"name\":\"A\",\"first_release_date\":1600000000},{\"id\":2,\"name\":\"B\",\"first_release_date\":1500000000},{\"id\":3,\"name\":\"C\",\"first_release_date\":1400000000}]"));
                    }));
            });
        });
        var report = await LiveSmokeScenario.RunAsync(host, counters, true, true, "simulated");
        Assert.Equal("PASSED", report.Result);
        Assert.Equal(1, report.OAuthRequests);
        Assert.Equal(3, report.IgdbRequests);
        Assert.Equal(6, report.Steps.Count);
        Assert.Equal(report.Steps[2].FetchedAt, report.Steps[3].FetchedAt);
        Assert.All(report.Steps, step => Assert.Equal(200, step.Status));
    }
}
