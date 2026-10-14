using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Validation;

public sealed class SwaggerUiTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Swagger_page_and_initializer_use_existing_openapi_without_persisting_authorization(string environment)
    {
        await using var host = new TestApiFactory()
            .WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/swagger/index.html");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("swagger-ui", html);
        Assert.Contains("TCC Games API", html);
        using var initializer = await client.GetAsync("/swagger/index.js");
        Assert.Equal(200, (int)initializer.StatusCode);
        var javascript = await initializer.Content.ReadAsStringAsync();
        Assert.Contains("../openapi/v1.json", javascript);
        Assert.DoesNotContain("\"persistAuthorization\":true", javascript);
        Assert.DoesNotContain("/swagger/v1/swagger.json", javascript);
        using var document = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(200, (int)document.StatusCode);
    }

    [Theory]
    [InlineData("/swagger/swagger-ui.css", "text/css")]
    [InlineData("/swagger/swagger-ui-bundle.js", "text/javascript")]
    [InlineData("/swagger/swagger-ui-standalone-preset.js", "text/javascript")]
    public async Task Swagger_assets_are_served_by_backend_without_a_CDN(string path, string mediaType)
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Swagger_short_url_redirects_to_the_page()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/swagger");
        Assert.Equal(301, (int)response.StatusCode);
        Assert.Equal("swagger/index.html", response.Headers.Location?.OriginalString);
    }
}
