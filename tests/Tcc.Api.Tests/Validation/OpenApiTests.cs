using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests.Validation;

public sealed class OpenApiTests
{
    [Fact]
    public async Task Comment_like_contract_has_private_likedByMe_no_write_body_and_public_nonnegative_count()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var writes = paths.GetProperty("/api/v1/comments/{commentId}/like");
        foreach (var method in new[] { "put", "delete", "get" })
        {
            var operation = method == "get" ? paths.GetProperty("/api/v1/comments/{commentId}/like/me").GetProperty(method) : writes.GetProperty(method);
            Assert.NotEmpty(operation.GetProperty("security").EnumerateArray());
            var responses = operation.GetProperty("responses");
            foreach (var statusCode in new[] { "400", "401", "404", "503" }) Assert.True(responses.TryGetProperty(statusCode, out _));
            var id = Assert.Single(operation.GetProperty("parameters").EnumerateArray(), parameter => parameter.GetProperty("name").GetString() == "commentId").GetProperty("schema");
            Assert.Equal("integer", id.GetProperty("type").GetString());
            Assert.Equal("int32", id.GetProperty("format").GetString());
            Assert.Equal(1, id.GetProperty("minimum").GetInt32());
            if (method == "get") Assert.Contains("no-store", operation.GetProperty("description").GetString());
            else
            {
                Assert.False(operation.TryGetProperty("requestBody", out _));
                Assert.False(responses.GetProperty("204").TryGetProperty("content", out _));
            }
        }
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var status = schemas.GetProperty("CommentLikeStatusResponse").GetProperty("properties");
        Assert.Equal("likedByMe", Assert.Single(status.EnumerateObject()).Name);
        Assert.Equal("boolean", status.GetProperty("likedByMe").GetProperty("type").GetString());
        var comment = schemas.GetProperty("CommentResponse").GetProperty("properties");
        Assert.False(comment.TryGetProperty("likedByMe", out _));
        var count = comment.GetProperty("likeCount");
        Assert.Equal("integer", count.GetProperty("type").GetString());
        Assert.Equal("int32", count.GetProperty("format").GetString());
        Assert.Equal(0, count.GetProperty("minimum").GetInt32());
    }

    [Fact]
    public async Task Profile_patch_is_partial_and_returns_private_completion_without_changing_public_user_schema()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var patch = schemas.GetProperty("ProfilePatchRequest");
        Assert.False(patch.TryGetProperty("required", out var required) && required.GetArrayLength() > 0);
        var ids = patch.GetProperty("properties").GetProperty("favoriteGameIds");
        Assert.Equal("array", ids.GetProperty("type").GetString());
        Assert.Equal(0, ids.GetProperty("minItems").GetInt32());
        Assert.Equal(1, ids.GetProperty("items").GetProperty("minimum").GetInt32());
        Assert.Equal(new[] { "avatarUrl", "bio", "completion", "displayName", "id", "username" },
            schemas.GetProperty("ProfileUpdateResponse").GetProperty("properties").EnumerateObject().Select(item => item.Name).Order());
        Assert.False(schemas.GetProperty("PublicUserResponse").GetProperty("properties").TryGetProperty("completion", out _));
        Assert.True(root.GetProperty("paths").GetProperty("/api/v1/users/me/profile-completion").GetProperty("put").GetProperty("deprecated").GetBoolean());
    }

    [Fact]
    public async Task Review_like_contract_has_no_write_body_public_integer_count_and_private_boolean_status()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var like = root.GetProperty("paths").GetProperty("/api/v1/reviews/{reviewId}/like");
        foreach (var method in new[] { "put", "delete" })
        {
            Assert.False(like.GetProperty(method).TryGetProperty("requestBody", out _));
            Assert.True(like.GetProperty(method).GetProperty("responses").TryGetProperty("204", out _));
        }
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var review = schemas.GetProperty("ReviewResponse").GetProperty("properties");
        Assert.False(review.TryGetProperty("likedByMe", out _));
        var count = review.GetProperty("likeCount");
        Assert.Equal("integer", count.GetProperty("type").GetString());
        Assert.Equal("int32", count.GetProperty("format").GetString());
        Assert.Equal(0, count.GetProperty("minimum").GetInt32());
        var status = schemas.GetProperty("ReviewLikeStatusResponse");
        Assert.Equal("likedByMe", Assert.Single(status.GetProperty("properties").EnumerateObject()).Name);
        Assert.Equal("boolean", status.GetProperty("properties").GetProperty("likedByMe").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Unified_review_documents_required_creation_rating_partial_edit_and_legacy_nullable_output()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var create = schemas.GetProperty("ReviewCreateRequest");
        Assert.Equal("rating", Assert.Single(create.GetProperty("required").EnumerateArray()).GetString());
        var rating = create.GetProperty("properties").GetProperty("rating");
        Assert.Equal("number", rating.GetProperty("type").GetString());
        Assert.Equal(0, rating.GetProperty("minimum").GetDouble());
        Assert.Equal(5, rating.GetProperty("maximum").GetDouble());
        Assert.False(create.GetProperty("additionalProperties").GetBoolean());
        var patch = schemas.GetProperty("ReviewPatchRequest");
        Assert.False(patch.TryGetProperty("required", out var required) && required.GetArrayLength() > 0);
        Assert.Equal(new[] { "rating", "text", "title" }, patch.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order());
        var nullable = schemas.GetProperty("ReviewResponse").GetProperty("properties").GetProperty("rating");
        Assert.Contains("null", nullable.GetProperty("type").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task Completion_contract_distinguishes_optional_avatar_from_required_private_fields()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var input = schemas.GetProperty("ProfileCompletionRequest");
        Assert.Equal(new[] { "favoriteGameIds" }, input.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Order());
        Assert.False(input.GetProperty("additionalProperties").GetBoolean());
        var properties = input.GetProperty("properties");
        Assert.False(properties.TryGetProperty("avatarSpecified", out _));
        var ids = properties.GetProperty("favoriteGameIds");
        Assert.Equal(0, ids.GetProperty("minItems").GetInt32());
        Assert.Equal("integer", ids.GetProperty("items").GetProperty("type").GetString());
        var output = schemas.GetProperty("ProfileCompletionDetails");
        Assert.Equal(new[] { "avatarUrl", "bio", "country", "favoriteGameCount", "favoriteGameIds", "isComplete", "state" }, output.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Order());
        Assert.Equal("boolean", output.GetProperty("properties").GetProperty("isComplete").GetProperty("type").GetString());
    }

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
                if (route.Path == "/api/v1/search" && status == 503)
                {
                    Assert.Contains("SearchResponse", error.GetProperty("schema").ToString());
                    continue;
                }
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
        foreach (var name in new[] { "PublicUserResponse", "GameResponse", "ReviewResponse", "RatingResponse", "CommentResponse", "FavoriteResponse", "NotificationResponse" })
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
