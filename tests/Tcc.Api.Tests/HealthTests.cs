using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests;

public sealed class HealthTests
{
    [Fact]
    public async Task Health_returns_typed_success()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthContract>();
        Assert.Equal("ok", body?.Status);
    }

    private sealed record HealthContract(string Status);
}
