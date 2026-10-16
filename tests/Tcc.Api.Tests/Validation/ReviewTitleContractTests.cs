using System.Text.Json;

namespace Tcc.Api.Tests.Validation;

public sealed class ReviewTitleContractTests
{
    [Fact]
    public async Task Openapi_exposes_nullable_title_limit_and_conditional_validation_without_changing_required_rating()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "ReviewCreateRequest", "ReviewPatchRequest", "ReviewResponse" })
        {
            var title = schemas.GetProperty(name).GetProperty("properties").GetProperty("title");
            Assert.Contains("null", title.GetProperty("type").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal(150, title.GetProperty("maxLength").GetInt32());
        }
        Assert.Equal("rating", Assert.Single(schemas.GetProperty("ReviewCreateRequest").GetProperty("required").EnumerateArray()).GetString());
        var patch = schemas.GetProperty("ReviewPatchRequest");
        Assert.False(patch.TryGetProperty("required", out var required) && required.GetArrayLength() > 0);
    }
}
