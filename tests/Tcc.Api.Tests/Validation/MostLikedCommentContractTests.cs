using System.Text.Json;

namespace Tcc.Api.Tests.Validation;

public sealed class MostLikedCommentContractTests
{
    [Fact]
    public async Task Openapi_exposes_public_pagination_exact_sizes_context_and_nonnegative_counts()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = json.RootElement;
        var operation = root.GetProperty("paths").GetProperty("/api/v1/community/comments/most-liked").GetProperty("get");
        Assert.False(operation.TryGetProperty("security", out var security) && security.GetArrayLength() > 0);
        foreach (var code in new[] { "200", "400", "503" }) Assert.True(operation.GetProperty("responses").TryGetProperty(code, out _));
        var parameters = operation.GetProperty("parameters").EnumerateArray().ToDictionary(item => item.GetProperty("name").GetString()!, item => item.GetProperty("schema"));
        Assert.Equal(new[] { "24h", "7d", "30d" }, parameters["period"].GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("7d", parameters["period"].GetProperty("default").GetString());
        Assert.Equal(1, parameters["page"].GetProperty("minimum").GetInt32());
        Assert.Equal(new[] { 10, 20, 50 }, parameters["pageSize"].GetProperty("enum").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(20, parameters["pageSize"].GetProperty("default").GetInt32());
        var schema = root.GetProperty("components").GetProperty("schemas").GetProperty("RankedCommentResponse").GetProperty("properties");
        foreach (var field in new[] { "commentId", "reviewId", "gameId", "likeCount", "periodLikeCount" })
        {
            Assert.Equal("integer", schema.GetProperty(field).GetProperty("type").GetString());
            Assert.Equal("int32", schema.GetProperty(field).GetProperty("format").GetString());
        }
        Assert.Equal(0, schema.GetProperty("likeCount").GetProperty("minimum").GetInt32());
        Assert.Equal(1, schema.GetProperty("periodLikeCount").GetProperty("minimum").GetInt32());
        Assert.False(schema.TryGetProperty("likedByMe", out _));
        Assert.Contains("null", schema.GetProperty("parentCommentId").GetProperty("type").EnumerateArray().Select(item => item.GetString()));
    }
}
