using Tcc.Api.Configuration;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Igdb;

public sealed class LocalEnvironmentTests
{
    [Fact]
    public void Reads_local_credentials_quotes_comments_and_equals_without_expansion()
    {
        using var file = new EnvironmentFile("# Local only\nIGDB__ClientId=123\nIGDB__ClientSecret=\"synthetic=a#b\"\nIGDB__TokenRefreshMarginSeconds=15\nOTHER_KEY=ignored");
        var options = IgdbCredentialOptions.Read(LocalIgdbEnvironment.CreateReader(file.Path, _ => null));
        Assert.Equal("123", options.ClientId);
        Assert.Equal("synthetic=a#b", options.ClientSecret);
        Assert.Equal(TimeSpan.FromSeconds(15), options.RefreshMargin);
    }

    [Fact]
    public void Process_variables_override_file_and_are_not_modified()
    {
        using var file = new EnvironmentFile("IGDB__ClientId=local-id\nIGDB__ClientSecret=local-secret");
        var variables = new Dictionary<string, string?> { ["IGDB__ClientId"] = "process-id", ["IGDB__ClientSecret"] = "process-secret" };
        var options = IgdbCredentialOptions.Read(LocalIgdbEnvironment.CreateReader(file.Path, name => variables.GetValueOrDefault(name)));
        Assert.Equal("process-id", options.ClientId);
        Assert.Equal("process-secret", options.ClientSecret);
        Assert.Equal(2, variables.Count);
    }

    [Fact]
    public void Missing_file_preserves_safe_missing_credentials_error()
    {
        var reader = LocalIgdbEnvironment.CreateReader(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".env"), _ => null);
        var failure = Assert.Throws<InvalidOperationException>(() => IgdbCredentialOptions.Read(reader));
        Assert.Contains("IGDB__", failure.Message);
    }

    [Theory]
    [InlineData("IGDB__ClientId=123\nIGDB__ClientSecret=\"synthetic-secret")]
    [InlineData("IGDB__ClientId=123\nIGDB__ClientSecret=synthetic-secret\nIGDB__ClientSecret=duplicate")]
    public void Invalid_file_fails_without_echoing_its_content(string content)
    {
        using var file = new EnvironmentFile(content);
        var failure = Assert.Throws<InvalidOperationException>(() => IgdbCredentialOptions.Read(LocalIgdbEnvironment.CreateReader(file.Path, _ => null)));
        Assert.DoesNotContain("synthetic-secret", failure.ToString());
        Assert.DoesNotContain(file.Path, failure.ToString());
    }
}

internal sealed class EnvironmentFile : IDisposable
{
    public string Path { get; } = System.IO.Path.GetTempFileName();
    public EnvironmentFile(string content) => File.WriteAllText(Path, content);
    public void Dispose() => File.Delete(Path);
}
