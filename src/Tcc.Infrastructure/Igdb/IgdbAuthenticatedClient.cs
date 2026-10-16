using System.Text;
using System.Net;
using System.Net.Http.Headers;
using Tcc.Application.Catalog;

namespace Tcc.Infrastructure.Igdb;

public sealed class IgdbAuthenticatedClient(HttpClient http, IgdbCredentialOptions options, TwitchTokenProvider tokens,
    IgdbResiliencePolicy? resilience = null, ExternalEndpointOptions? endpoints = null) : IDisposable
{
    private readonly Uri baseUrl = (endpoints ?? ExternalEndpointOptions.Default).BaseUrl;
    private readonly IgdbResiliencePolicy policy = resilience ?? new(new(), new SystemIgdbScheduler(TimeProvider.System));
    public Task<HttpResponseMessage> PostGamesAsync(string query, CancellationToken cancellationToken = default) => SendAsync("games", query, cancellationToken);
    public Task<HttpResponseMessage> PostGamesCountAsync(string query, CancellationToken cancellationToken) => SendAsync("games/count", query, cancellationToken);
    public Task<HttpResponseMessage> PostGenresAsync(string query, CancellationToken cancellationToken) => SendAsync("genres", query, cancellationToken);
    public Task<HttpResponseMessage> PostPlatformsAsync(string query, CancellationToken cancellationToken) => SendAsync("platforms", query, cancellationToken);
    private async Task<HttpResponseMessage> SendAsync(string endpoint, string query, CancellationToken cancellationToken)
    {
        using var budget = policy.CreateBudget(cancellationToken);
        try
        {
            budget.Check();
            var token = await tokens.GetAsync(budget.Token);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var response = await policy.SendAsync(async ct =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUrl, endpoint));
                    request.Headers.Add("Client-ID", options.ClientId);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
                    request.Content = new StringContent(query, Encoding.UTF8, "text/plain");
                    return await http.SendAsync(request, ct);
                }, budget);
                if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
                response.Dispose();
                if (attempt == 0) token = await tokens.RefreshRejectedAsync(token, budget.Token);
                else tokens.InvalidateRejected(token);
            }
            throw new ExternalIntegrationUnavailableException(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException(cancellationToken); }
        catch (OperationCanceledException) { throw new CatalogTimeoutException(); }
        catch (HttpRequestException) { throw new ExternalIntegrationUnavailableException(); }
    }
    public void Dispose() { http.Dispose(); if (resilience is null) policy.Dispose(); }
}
