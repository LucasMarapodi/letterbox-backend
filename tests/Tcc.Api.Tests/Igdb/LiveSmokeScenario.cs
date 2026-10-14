using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Igdb;

internal sealed record SmokeStep(string Name, int Status, int OAuthRequests, int IgdbRequests, string? GameId = null,
    bool? Stale = null, DateTimeOffset? FetchedAt = null);
internal sealed record SmokeReport(string Mode, string Result, string Reason, DateTimeOffset At, int OAuthRequests,
    int IgdbRequests, IReadOnlyList<SmokeStep> Steps);
internal sealed class SmokeCounters
{
    public int OAuthRequests { get; private set; }
    public int IgdbRequests { get; private set; }
    public void Record(bool oauth)
    {
        if (oauth) OAuthRequests++; else IgdbRequests++;
        if (OAuthRequests > 2 || IgdbRequests > 6) throw new HttpRequestException("Orçamento do smoke esgotado.");
    }
}
internal static class LiveSmokeScenario
{
    public static Task<SmokeReport> RunAsync(WebApplicationFactory<Program> host, SmokeCounters counters,
        bool requested, bool credentialsReady, string mode, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmokeReport(mode, "FAILED", "not_implemented", DateTimeOffset.UtcNow, 0, 0, []));
}
