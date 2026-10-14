using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Tests.Opinions;
using Tcc.Application.Catalog;
using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Domain.Opinions;

namespace Tcc.Api.Tests.Igdb;

public sealed class AsyncWriteTests
{
    [Fact]
    public async Task Delayed_catalog_does_not_hold_publication_lock_and_concurrent_reviews_are_unique()
    {
        var source = new WriteCatalog();
        await using var host = CatalogHttpTests.Host(source);
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var first = client.PostAsJsonAsync("/api/v1/games/igdb-10/reviews", new TextInput("A"));
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var read = Task.Run(() => host.Services.GetRequiredService<IPublicationScope>().Execute(() =>
            host.Services.GetRequiredService<IReviews>().All().Count));
        await read.WaitAsync(TimeSpan.FromSeconds(3));
        var second = client.PostAsJsonAsync("/api/v1/games/igdb-10/reviews", new TextInput("B"));
        source.Release.TrySetResult();
        var responses = await Task.WhenAll(first, second);
        try { Assert.Equal(new[] { 201, 409 }, responses.Select(response => (int)response.StatusCode).Order()); }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Single(host.Services.GetRequiredService<IReviews>().All(), review => review.GameId == "igdb-10");
    }

    [Fact]
    public async Task Expired_session_is_revalidated_after_external_wait()
    {
        var source = new WriteCatalog();
        await using var host = CatalogHttpTests.Host(source);
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        var publish = client.PostAsJsonAsync("/api/v1/games/igdb-10/reviews", new TextInput("A"));
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var logout = await client.PostAsync("/api/v1/auth/logout", null);
        source.Release.TrySetResult();
        using var response = await publish;
        Assert.Equal(401, (int)response.StatusCode);
        Assert.DoesNotContain(host.Services.GetRequiredService<IReviews>().All(), review => review.GameId == "igdb-10");
    }

    [Fact]
    public async Task Cancellation_before_commit_does_not_publish()
    {
        var source = new WriteCatalog();
        await using var host = CatalogHttpTests.Host(source);
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        using var cancellation = new CancellationTokenSource();
        var publish = client.PostAsJsonAsync("/api/v1/games/igdb-10/reviews", new TextInput("A"), cancellation.Token);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publish);
        source.Release.TrySetResult();
        Assert.DoesNotContain(host.Services.GetRequiredService<IReviews>().All(), review => review.GameId == "igdb-10");
    }

    [Fact]
    public async Task Projection_failure_before_edit_preserves_text_and_success_does_not_refetch_after_write()
    {
        var source = new WriteCatalog { Fail = true };
        source.Release.TrySetResult();
        await using var host = CatalogHttpTests.Host(source);
        var store = host.Services.GetRequiredService<IReviews>();
        store.TryAdd(Review.Create("edit", "user-1", "igdb-10", "Original", CatalogFlow.Metadata.FetchedAt));
        using var client = host.CreateClient();
        await ApiTest.SignIn(client);
        using var failed = await client.PatchAsJsonAsync("/api/v1/reviews/edit", new TextInput("Changed"));
        Assert.Equal(503, (int)failed.StatusCode);
        Assert.Equal("Original", store.Find("edit")?.Text);
        source.Fail = false;
        var calls = source.Calls;
        using var success = await client.PatchAsJsonAsync("/api/v1/reviews/edit", new TextInput("Changed"));
        Assert.Equal(200, (int)success.StatusCode);
        Assert.Equal(calls + 1, source.Calls);
        Assert.Equal("Changed", store.Find("edit")?.Text);
    }
}

internal sealed class WriteCatalog : IAsyncGameCatalog
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Fail { get; set; }
    public int Calls { get; private set; }
    public async Task<CatalogGame?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        Calls++; Entered.TrySetResult();
        await Release.Task.WaitAsync(cancellationToken);
        if (Fail) throw new CatalogIntegrationUnavailableException();
        return CatalogFlow.Game(id);
    }
    public Task<CatalogGame?> RefreshAsync(string id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);
    public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
