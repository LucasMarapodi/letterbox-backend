using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Application;
using Tcc.Domain;

namespace Tcc.Api.Tests.Persistence;

public sealed class AggregatedSearchBehaviorTests
{
    [Theory]
    [InlineData("people", 1, 0)]
    [InlineData("games", 0, 1)]
    [InlineData("all", 1, 1)]
    public async Task Scope_queries_only_requested_sources_sequentially(string scope, int peopleCalls, int gameCalls)
    {
        var people = new PeopleProbe();
        var games = new GameProbe(() => Assert.False(people.Active));
        var result = await new AggregatedSearch(people, games).Execute(SearchRequest.Create("query", scope: scope));
        Assert.True(result.IsAvailable);
        Assert.Equal(peopleCalls, people.Calls);
        Assert.Equal(gameCalls, games.Calls);
        if (gameCalls == 1) Assert.True(result.Games.Metadata!.IsStale);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task People_failure_keeps_games_or_total_failure_reports_both_errors(bool failGames)
    {
        var result = await new AggregatedSearch(new PeopleProbe { Fail = true }, new GameProbe(() => { }) { Fail = failGames })
            .Execute(SearchRequest.Create("query"));
        Assert.Equal("storage_unavailable", result.People.Error?.Code);
        Assert.Equal(!failGames, result.IsAvailable);
        Assert.Equal(failGames ? SearchStatus.Unavailable : SearchStatus.Ready, result.Games.Status);
        Assert.Equal(failGames ? "search_unavailable" : null, result.Error?.Code);
        Assert.Null(result.People.Page);
    }

    [Fact]
    public async Task Caller_cancellation_stops_before_starting_the_next_source()
    {
        using var cancellation = new CancellationTokenSource();
        var people = new PeopleProbe { After = cancellation.Cancel };
        var games = new GameProbe(() => { });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AggregatedSearch(people, games)
            .Execute(SearchRequest.Create("query"), cancellation.Token));
        Assert.Equal(1, people.Calls);
        Assert.Equal(0, games.Calls);
    }

    [Fact]
    public void Query_limit_is_validated_after_trim_and_before_work()
    {
        Assert.Equal(200, SearchRequest.Create(" " + new string('x', 200) + " ").Query.Length);
        Assert.Throws<DomainValidationException>(() => SearchRequest.Create(new string('x', 201)));
    }

    private sealed class PeopleProbe : IPeopleSearch
    {
        public int Calls { get; private set; }
        public bool Active { get; private set; }
        public bool Fail { get; init; }
        public Action? After { get; init; }
        public async Task<PeopleSearchPage> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            Active = true;
            try
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                if (Fail) throw new DatabaseUnavailableException();
                After?.Invoke();
                return new([], false);
            }
            finally { Active = false; }
        }
    }
    private sealed class GameProbe(Action before) : IAsyncGameCatalog
    {
        public int Calls { get; private set; }
        public bool Fail { get; init; }
        public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken)
        {
            before(); Calls++;
            if (Fail) throw new CatalogIntegrationUnavailableException();
            return Task.FromResult(new CatalogPage(query, [], false, new(DateTimeOffset.UtcNow, true)));
        }
        public Task<CatalogGame?> GetAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogGame?> RefreshAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, CatalogGame>> ResolveAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
