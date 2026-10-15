using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Tests.Identity;
using Tcc.Api.Tests.Opinions;
using Tcc.Api.Tests.Social;
using Tcc.Application.Opinions;
using Tcc.Domain.Opinions;

namespace Tcc.Api.Tests.Feeds;

public sealed class RecentTests
{
    [Fact]
    public async Task Recent_is_public_includes_unfollowed_authors_and_does_not_reorder_edits_or_comments()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
        await using var host = new IdentityFactory(clock);
        using var client = host.CreateClient();
        await SocialTest.Register(client, "B");
        await ApiTest.SignIn(client);
        var ana = await ApiTest.Publish(client);
        await ApiTest.SignIn(client, "B@example.com", "p");
        var b = await ApiTest.Publish(client);
        var before = await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/community/reviews/recent");
        Assert.NotNull(before);
        Assert.Equal(new[] { ana.Id, b.Id }.Order().Append(1), before.Select(review => review.Id));
        await ApiTest.SignIn(client);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        using var edit = await client.PatchAsJsonAsync("/api/v1/reviews/1", new TextInput("Edited old review"));
        using var comment = await client.PostAsJsonAsync("/api/v1/reviews/1/comments", new TextInput("New comment"));
        edit.EnsureSuccessStatusCode(); comment.EnsureSuccessStatusCode();
        var authenticated = await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/community/reviews/recent");
        Assert.NotNull(authenticated);
        Assert.Equal(before.Select(review => review.Id), authenticated.Select(review => review.Id));
        Assert.Equal("Edited old review", authenticated[^1].Text);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(authenticated, await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/community/reviews/recent"));
    }

    [Fact]
    public async Task Empty_review_store_returns_an_empty_public_list()
    {
        await using var host = new EmptyReviewsFactory();
        using var client = host.CreateClient();
        var result = await client.GetFromJsonAsync<ReviewDto[]>("/api/v1/community/reviews/recent");
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}

public sealed class EmptyReviewsFactory : TestApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) { base.ConfigureWebHost(builder); builder.ConfigureServices(services => services.AddSingleton<IReviews, EmptyReviews>()); }
}
public sealed class EmptyReviews : IReviews
{
    public Task<Review?> AddAsync(Review review, CancellationToken ct = default, Func<Review, CancellationToken, Task>? publish = null) => Task.FromResult<Review?>(null);
    public Task<Review?> FindAsync(int id, CancellationToken ct = default) => Task.FromResult<Review?>(null);
    public Task<IReadOnlyList<Review>> QueryAsync(ReviewQuery query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Review>>([]);
    public Task<Review?> EditAsync(int id, int actorId, string? text, DateTimeOffset now, CancellationToken ct = default) => Task.FromResult<Review?>(null);
}