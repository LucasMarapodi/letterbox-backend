using System.Collections.Concurrent;
using System.Net;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tcc.Api.Configuration;
using Tcc.Api.Tests.Opinions;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class SecurityRegressionTests
{
    [Theory]
    [InlineData("oauth", 302, 503)]
    [InlineData("oauth", 400, 503)]
    [InlineData("igdb", 302, 502)]
    [InlineData("igdb", 401, 503)]
    [InlineData("igdb", 429, 503)]
    [InlineData("igdb", 500, 503)]
    [InlineData("igdb", 200, 502)]
    public async Task Supplier_faults_do_not_leak_sentinels_in_logs_headers_errors_or_openapi(string stage, int status, int expected)
    {
        const string sentinel = "synthetic-security-sentinel-GPR06";
        using var logs = new SecurityLogs();
        var calls = 0;
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(identityBuilder => identityBuilder.ConfigureServices(identityServices => Tcc.Api.Tests.Fixtures.TestIdentityRegistration.AddTestIdentity(identityServices))).WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(IgdbTest.Options());
                services.AddSingleton(new IgdbResilienceOptions { MaxRetries = 0 });
                services.AddHttpClient(IgdbAuthenticationRegistration.OAuthClientName).ConfigurePrimaryHttpMessageHandler(() =>
                    new TestHandler((request, _) =>
                    {
                        Assert.Equal("", request.RequestUri?.Query);
                        Assert.Equal("id.twitch.tv", request.RequestUri?.Host);
                        return Task.FromResult(stage == "oauth" ? Fault(status, sentinel) : IgdbTest.TokenResponse(sentinel));
                    }));
                services.AddHttpClient(IgdbAuthenticationRegistration.IgdbClientName).ConfigurePrimaryHttpMessageHandler(() =>
                    new TestHandler((request, _) =>
                    {
                        Interlocked.Increment(ref calls);
                        Assert.Equal("https://api.igdb.com/v4/games", request.RequestUri?.AbsoluteUri);
                        return Task.FromResult(Fault(status, sentinel));
                    }));
            });
        });
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        using var response = await client.GetAsync("/api/v1/games?pageSize=2");
        Assert.Equal(expected, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var headers = response.Headers.ToString() + response.Content.Headers;
        var openapi = await client.GetStringAsync("/openapi/v1.json");
        Assert.True(!body.Contains(sentinel, StringComparison.Ordinal), "Sentinela no erro público.");
        Assert.True(!headers.Contains(sentinel, StringComparison.Ordinal), "Sentinela nos headers públicos.");
        Assert.True(!openapi.Contains(sentinel, StringComparison.Ordinal), "Sentinela no OpenAPI.");
        Assert.True(logs.Messages.All(message => !message.Contains(sentinel, StringComparison.Ordinal)), "Sentinela nos logs.");
        Assert.False(response.Headers.Contains("X-Catalog-Stale"));
        using var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(200, (int)me.StatusCode);
        Assert.InRange(calls, 0, 2);
    }

    [Fact]
    public void Production_dependencies_are_scoped_and_exclude_test_assemblies_and_fake_catalog()
    {
        var root = WorkspaceRoot();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(path);
            var packages = document.Descendants("PackageReference").Select(item => item.Attribute("Include")?.Value).ToArray();
            Assert.All(packages, name => Assert.True(name is "Microsoft.AspNetCore.OpenApi" or "Swashbuckle.AspNetCore.SwaggerUI" or "Microsoft.EntityFrameworkCore" or "Microsoft.EntityFrameworkCore.Relational" or "Microsoft.EntityFrameworkCore.Design" or "Npgsql.EntityFrameworkCore.PostgreSQL", "Dependência externa inesperada no backend."));
            Assert.All(document.Descendants("ProjectReference"), item => Assert.DoesNotContain("Tests", item.Attribute("Include")?.Value ?? ""));
        }
        Assert.DoesNotContain(typeof(IgdbGameCatalog).Assembly.GetTypes(), type => type.Name.Contains("Fake", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(Program).Assembly.GetReferencedAssemblies(), reference => reference.Name?.Contains("Tests", StringComparison.Ordinal) == true);
        var infrastructure = XDocument.Load(Path.Combine(root, "src/Tcc.Infrastructure/Tcc.Infrastructure.csproj"));
        Assert.EndsWith("Tcc.Application.csproj", Assert.Single(infrastructure.Descendants("ProjectReference")).Attribute("Include")?.Value);
    }

    internal static string WorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tcc.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Workspace de validação ausente.");
    }
    private static HttpResponseMessage Fault(int status, string sentinel)
    {
        var response = IgdbTest.Json(sentinel, (HttpStatusCode)status);
        response.Headers.Location = new Uri("https://example.invalid/" + sentinel);
        return response;
    }
}

internal sealed class SecurityLogs : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Capture(Messages);
    public void Dispose() { }
    private sealed class Capture(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception) + exception);
    }
}
