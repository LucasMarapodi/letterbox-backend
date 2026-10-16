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
        Assert.True(reviews.TryAdd(Review.Create(101, 1, 1, "Old A", now.AddDays(-100), "Fixture review")));
        Assert.True(reviews.TryAdd(Review.Create(102, b.Id, 1, "Old B", now.AddDays(-100), "Fixture review")));
        Assert.True(reviews.TryAdd(Review.Create(103, 1, 2, "Old C", now.AddDays(-100), "Fixture review")));
    }
    public static void Comment(WebApplicationFactory<Program> host, int id, int reviewId, DateTimeOffset date) =>
        host.Services.GetRequiredService<IComments>().Add(Tcc.Domain.Opinions.Comment.Create(id, 1, reviewId, "Comment", date));
}
