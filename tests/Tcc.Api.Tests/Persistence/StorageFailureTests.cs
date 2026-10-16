using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Tcc.Api.DependencyInjection;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Igdb;

namespace Tcc.Api.Tests.Persistence;

public sealed class StorageFailureTests
{
    [Fact]
    public async Task Unavailable_database_returns_safe_503_without_activating_a_memory_account_or_leaking_configuration()
    {
        const string sentinel = "synthetic-database-secret";
        using var logs = new SecurityLogs();
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(logs));
            builder.ConfigureServices(services => services.AddPostgresPersistence(_ =>
                "Host=127.0.0.1;Port=1;Database=unavailable;Username=synthetic;Password=" + sentinel + ";Timeout=1"));
        });
        using var client = host.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginInput("ana@example.com", "teste123"));
        Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal("storage_unavailable", (await response.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        using var reviews = await client.GetAsync("/api/v1/community/reviews/recent");
        Assert.Equal(503, (int)reviews.StatusCode);
        Assert.Equal("storage_unavailable", (await reviews.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        using var ranked = await client.GetAsync("/api/v1/community/reviews/most-commented");
        Assert.Equal(503, (int)ranked.StatusCode);
        Assert.Equal("storage_unavailable", (await ranked.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        using var network = await client.GetAsync("/api/v1/users/1/followers");
        Assert.Equal(503, (int)network.StatusCode);
        Assert.Equal("storage_unavailable", (await network.Content.ReadFromJsonAsync<ErrorResult>())?.Code);
        using var health = await client.GetAsync("/health");
        Assert.Equal(200, (int)health.StatusCode);
        var openapi = await client.GetStringAsync("/openapi/v1.json");
        Assert.True(!openapi.Contains(sentinel, StringComparison.Ordinal), "Documento não deve conter configuração privada.");
        Assert.True(logs.Messages.All(message => !message.Contains(sentinel, StringComparison.Ordinal)), "Logs não devem conter configuração privada.");
    }
}
