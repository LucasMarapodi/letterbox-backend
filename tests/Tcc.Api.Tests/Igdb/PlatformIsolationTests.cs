using Tcc.Api.DependencyInjection;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Contracts;
using Tcc.Api.Tests.Opinions;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class PlatformIsolationTests
{
    [Fact]
    public async Task Persistent_supplier_401_becomes_safe_503_and_preserves_the_platform_session()
    {
        var oauthCalls = 0;
        var igdbCalls = 0;
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(testCatalogBuilder => testCatalogBuilder.ConfigureServices(testCatalogServices => Tcc.Api.Tests.Fixtures.TestCatalogRegistration.AddTestCatalog(testCatalogServices))).WithWebHostBuilder(identityBuilder => identityBuilder.ConfigureServices(identityServices => Tcc.Api.Tests.Fixtures.TestIdentityRegistration.AddTestIdentity(identityServices))).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton(IgdbTest.Options());
            services.AddControllers().AddApplicationPart(typeof(IgdbFailureProbeController).Assembly);
            services.AddHttpClient(IgdbAuthenticationRegistration.OAuthClientName).ConfigurePrimaryHttpMessageHandler(() =>
                new TestHandler((_, _) => { oauthCalls++; return Task.FromResult(IgdbTest.TokenResponse()); }));
            services.AddHttpClient(IgdbAuthenticationRegistration.IgdbClientName).ConfigurePrimaryHttpMessageHandler(() =>
                new TestHandler((_, _) => { igdbCalls++; return Task.FromResult(IgdbTest.Json("synthetic-supplier-secret", HttpStatusCode.Unauthorized)); }));
        }));
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        Assert.Equal(0, oauthCalls);
        using var failure = await client.GetAsync("/__tests/igdb/failure");
        Assert.Equal(503, (int)failure.StatusCode);
        var json = await failure.Content.ReadAsStringAsync();
        Assert.Contains("integration_unavailable", json);
        Assert.DoesNotContain("synthetic", json);
        Assert.DoesNotContain("Exception", json);
        Assert.Equal(2, oauthCalls);
        Assert.Equal(2, igdbCalls);
        using var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(200, (int)me.StatusCode);
    }

    [Fact]
    public async Task Production_has_no_external_token_or_probe_endpoint()
    {
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(testCatalogBuilder => testCatalogBuilder.ConfigureServices(testCatalogServices => Tcc.Api.Tests.Fixtures.TestCatalogRegistration.AddTestCatalog(testCatalogServices))).WithWebHostBuilder(identityBuilder => identityBuilder.ConfigureServices(identityServices => Tcc.Api.Tests.Fixtures.TestIdentityRegistration.AddTestIdentity(identityServices)));
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/__tests/igdb/failure");
        Assert.Equal(404, (int)response.StatusCode);
    }
}

[ApiController]
[Route("__tests/igdb")]
public sealed class IgdbFailureProbeController(IgdbAuthenticatedClient integration) : ControllerBase
{
    [HttpGet("failure")]
    public async Task<ActionResult<HealthResponse>> Failure(CancellationToken cancellationToken)
    {
        using var response = await integration.PostGamesAsync("fields id; limit 1;", cancellationToken);
        return Ok(new HealthResponse("unexpected-success"));
    }
}
