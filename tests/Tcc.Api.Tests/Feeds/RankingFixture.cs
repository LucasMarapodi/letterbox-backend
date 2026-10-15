using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Tests.Social;
using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;

namespace Tcc.Api.Tests.Feeds;

internal static class RankingFixture
{
    public static async Task SeedReviews(WebApplicationFactory<Program> host, HttpClient client, DateTimeOffset now)
    {
        var b = await SocialTest.Register(client, "B");
        var reviews = host.Services.GetRequiredService<IReviews>();
        Assert.True(reviews.TryAdd(Review.Create("rank-a", 1, 1, "Old A", now.AddDays(-100))));
        Assert.True(reviews.TryAdd(Review.Create("rank-b", b.Id, 1, "Old B", now.AddDays(-100))));
        Assert.True(reviews.TryAdd(Review.Create("rank-c", 1, 2, "Old C", now.AddDays(-100))));
    }
    public static void Comment(WebApplicationFactory<Program> host, string id, string reviewId, DateTimeOffset date) =>
        host.Services.GetRequiredService<IComments>().Add(Tcc.Domain.Opinions.Comment.Create(id, 1, reviewId, "Comment", date));
}
