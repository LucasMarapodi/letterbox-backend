using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tcc.Api.DependencyInjection;
using Tcc.Infrastructure.Igdb;
using Xunit.Abstractions;

namespace Tcc.Api.Tests.Igdb;

public sealed class LiveIgdbSmokeTests(ITestOutputHelper output)
{
    [LiveIgdbFact]
    [Trait("Category", "IgdbLive")]
    public async Task Real_catalog_public_API_cache_local_and_token_are_verified_with_limited_requests()
    {
        var counters = new SmokeCounters();
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            // Live credentials/payloads must never be included in runner logs.
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new IgdbResilienceOptions
                { RequestsPerSecond = 1, MaxConcurrent = 1, QueueLimit = 0, MaxRetries = 0, TotalTimeout = TimeSpan.FromSeconds(30) });
                services.AddHttpClient(IgdbAuthenticationRegistration.OAuthClientName)
                    .AddHttpMessageHandler(() => new CountRequests(counters, true));
                services.AddHttpClient(IgdbAuthenticationRegistration.IgdbClientName)
                    .AddHttpMessageHandler(() => new CountRequests(counters, false));
            });
        });
        var ready = false;
        try
        {
            var options = host.Services.GetRequiredService<IgdbCredentialOptions>();
            ready = options.ClientId != "123" && options.ClientSecret != "123";
        }
        catch (InvalidOperationException) { /* Missing or malformed local configuration is a blocked smoke. */ }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var report = await LiveSmokeScenario.RunAsync(host, counters, true, ready, "live", timeout.Token);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        var directory = Path.Combine(SecurityRegressionTests.WorkspaceRoot(), "docs/evidence");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "G-PR-06-live.json"), json, CancellationToken.None);
        output.WriteLine(json);
        Assert.True(report.Result == "PASSED", report.Result + ": " + report.Reason);
    }

    private sealed class CountRequests(SmokeCounters counters, bool oauth) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var expected = oauth ? "https://id.twitch.tv/oauth2/token" : "https://api.igdb.com/v4/games";
            if (request.Method != HttpMethod.Post || request.RequestUri?.AbsoluteUri != expected)
                throw new HttpRequestException("Destino do smoke inválido.");
            counters.Record(oauth);
            return base.SendAsync(request, cancellationToken);
        }
    }
}

public sealed class LiveIgdbFactAttribute : FactAttribute
{
    public LiveIgdbFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IGDB__RunLiveSmoke", EnvironmentVariableTarget.Process) != "1")
            Skip = "Smoke live opt-in: execute scripts/igdb-smoke.ps1 -Live separadamente da suíte determinística.";
    }
}
