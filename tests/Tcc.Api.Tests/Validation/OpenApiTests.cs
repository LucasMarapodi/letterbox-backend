using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Validation;

public sealed class OpenApiTests
{
    [Fact]
    public async Task Openapi_documents_all_operations_success_errors_and_exact_access_requirements()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(200, (int)response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("/", root.GetProperty("servers")[0].GetProperty("url").GetString());
        var paths = root.GetProperty("paths");
        var actual = paths.EnumerateObject().SelectMany(path => path.Value.EnumerateObject()
            .Where(operation => operation.Name is "get" or "post" or "put" or "patch" or "delete")
            .Select(operation => $"{operation.Name} {path.Name}")).Order();
        Assert.Equal(EndpointInventory.All.Select(route => $"{route.Method} {route.Path}").Order(), actual);
        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("opaque", scheme.GetProperty("bearerFormat").GetString());
        foreach (var route in EndpointInventory.All)
        {
            var operation = paths.GetProperty(route.Path).GetProperty(route.Method);
            var responses = operation.GetProperty("responses");
            Assert.True(responses.TryGetProperty(route.Success.ToString(), out var success), $"{route.Method} {route.Path}");
            if (route.Success == 204) Assert.False(success.TryGetProperty("content", out _));
            else Assert.True(success.GetProperty("content").GetProperty("application/json").TryGetProperty("schema", out _));
            var secured = operation.TryGetProperty("security", out var security) && security.GetArrayLength() > 0;
            Assert.Equal(route.Private, secured);
            if (secured) Assert.True(security[0].TryGetProperty("Bearer", out _));
            foreach (var status in route.Errors.Concat(route.Private ? new[] { 401 } : []).Append(500).Distinct())
            {
                var error = responses.GetProperty(status.ToString()).GetProperty("content").GetProperty("application/json");
                Assert.Contains("ApiError", error.GetProperty("schema").ToString());
                Assert.True(error.TryGetProperty("example", out _));
            }
        }
    }

    [Fact]
    public async Task Public_schemas_are_named_safe_and_requests_describe_validation_and_examples()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(200, (int)response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "PublicUserResponse", "GameResponse", "ReviewResponse", "RatingResponse", "CommentResponse" })
        {
            var id = schemas.GetProperty(name).GetProperty("properties").GetProperty("id");
            Assert.Equal("integer", id.GetProperty("type").GetString());
            Assert.Equal("int32", id.GetProperty("format").GetString());
        }
        var user = schemas.GetProperty("PublicUserResponse").GetProperty("properties");
        Assert.Equal(new[] { "avatarUrl", "bio", "displayName", "id", "username" }, user.EnumerateObject().Select(property => property.Name).Order());
        foreach (var schema in schemas.EnumerateObject().Where(schema => schema.Name.EndsWith("Response", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("password", schema.Value.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("passwordHash", schema.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        var password = schemas.GetProperty("RegisterRequest").GetProperty("properties").GetProperty("password");
        Assert.Equal("password", password.GetProperty("format").GetString());
        Assert.True(password.GetProperty("writeOnly").GetBoolean());
        Assert.True(schemas.GetProperty("RegisterRequest").GetProperty("examples").GetArrayLength() > 0);
        var value = schemas.GetProperty("RatingRequest").GetProperty("properties").GetProperty("value");
        Assert.Equal(0, value.GetProperty("minimum").GetInt32());
        Assert.Equal(5, value.GetProperty("maximum").GetInt32());
        var parameters = root.GetProperty("paths").GetProperty("/api/v1/community/reviews/most-commented")
            .GetProperty("get").GetProperty("parameters");
        var period = Assert.Single(parameters.EnumerateArray(), parameter => parameter.GetProperty("name").GetString() == "period");
        Assert.Equal("7d", period.GetProperty("schema").GetProperty("default").GetString());
        Assert.Equal(new[] { "24h", "7d", "30d" }, period.GetProperty("schema").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
    }
}
