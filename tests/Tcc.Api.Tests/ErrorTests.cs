using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Tcc.Api.Tests;

public sealed class ErrorTests
{
    [Fact]
    public async Task Serialization_preserves_opaque_id_and_null_and_normalizes_utc()
    {
        await using var host = new ProbeFactory();
        using var client = host.CreateClient();
        var input = new ProbePayload("opaque/id:value", new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.FromHours(-3)), null);
        using var response = await client.PostAsJsonAsync("/__tests/payload", input);
        response.EnsureSuccessStatusCode();
        var output = await response.Content.ReadFromJsonAsync<ProbePayload>();
        Assert.NotNull(output);
        Assert.Equal(input.Id, output.Id);
        Assert.Equal(input.CreatedAt, output.CreatedAt);
        Assert.Equal(TimeSpan.Zero, output.CreatedAt.Offset);
        Assert.Contains("\"optional\":null", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(401, "unauthorized")]
    [InlineData(403, "forbidden")]
    [InlineData(404, "not_found")]
    [InlineData(409, "conflict")]
    [InlineData(500, "internal_error")]
    public async Task Failures_have_safe_stable_contract(int status, string code)
    {
        await using var host = new ProbeFactory();
        using var client = host.CreateClient();
        using var response = await client.GetAsync($"/__tests/failure/{status}");
        Assert.Equal(status, (int)response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorContract>();
        Assert.Equal(code, error?.Code);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("Exception", json);
    }

    [Fact]
    public async Task Invalid_json_has_standard_error()
    {
        await using var host = new ProbeFactory();
        using var client = host.CreateClient();
        using var payload = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/__tests/payload", payload);
        Assert.Equal(400, (int)response.StatusCode);
        Assert.Equal("validation_failed", (await response.Content.ReadFromJsonAsync<ErrorContract>())?.Code);
    }

    [Fact]
    public async Task Production_does_not_expose_probes()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/__tests/failure/400");
        Assert.Equal(404, (int)response.StatusCode);
    }

    private sealed record ErrorContract(string Code, string Message);
}

public sealed class ProbeFactory : TestApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) { base.ConfigureWebHost(builder); builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly)); }
}

[ApiController]
[Route("__tests")]
public sealed class ProbeController : ControllerBase
{
    [HttpGet("failure/{status:int}")]
    public IActionResult Failure(int status) => status == 500
        ? throw new InvalidOperationException("secret")
        : StatusCode(status);

    [HttpPost("payload")]
    public ActionResult<ProbePayload> Payload(ProbePayload payload) => Ok(payload);
}

public sealed record ProbePayload(string Id, DateTimeOffset CreatedAt, string? Optional);
