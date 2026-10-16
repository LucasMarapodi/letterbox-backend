using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tcc.Application.Catalog;
using Tcc.Application.Opinions;
using Tcc.Domain.Catalog;
using Tcc.Domain.Opinions;

namespace Tcc.Api.Tests;

// Business fixtures belong exclusively to the test host. The real resolver/cache/local stores stay active.
public class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
    {
        Tcc.Api.Tests.Fixtures.TestIdentityRegistration.AddTestIdentity(services);
        Tcc.Api.Tests.Fixtures.TestCatalogRegistration.AddTestCatalog(services);
        services.AddSingleton<Tcc.Infrastructure.Memory.HostMemory>();
        services.AddSingleton<Tcc.Application.Memory.IHostSeed>(provider => provider.GetRequiredService<Tcc.Infrastructure.Memory.HostMemory>());
        services.AddSingleton<IExternalGameCatalog, FixtureCatalog>();
        services.AddSingleton<ICatalogFilterStore, FixtureCatalogFilters>();
        services.AddSingleton<IExternalCatalogFilters, FixtureCatalogFilters>();
        services.AddHostedService<CatalogFixtures>();
        services.AddHostedService<OpinionFixtures>();
    });
}

internal sealed class FixtureCatalogFilters : ICatalogFilterStore, IExternalCatalogFilters
{
    public Task<CatalogFilterOptions> ReadAsync(CancellationToken ct) => Task.FromResult(new CatalogFilterOptions([new(1, "Adventure")], [new(1, "PC")]));
    Task<ExternalCatalogFilters> IExternalCatalogFilters.ReadAsync(CancellationToken ct) => Task.FromResult(new ExternalCatalogFilters([new(1, "Adventure")], [new(1, "PC")]));
    public Task SynchronizeAsync(ExternalCatalogFilters filters, CancellationToken ct) => Task.CompletedTask;
    public Task<CatalogFilterBindings> ResolveAsync(int? genreId, int? platformId, CancellationToken ct) => Task.FromResult(new CatalogFilterBindings(genreId, platformId));
}

internal sealed class CatalogFixtures(ILocalGameCatalog catalog, TimeProvider clock) : IHostedService
{
    public Task StartAsync(CancellationToken ct) => catalog.SaveGamesAsync(FixtureCatalog.Games.Select(game => new CatalogGame(game, new(clock.GetUtcNow()))).ToArray(), ct);
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class OpinionFixtures(IReviews reviews, IComments comments) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var date = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        if (reviews.TryAdd(Review.Create(1, 1, 3, "Review fictícia de Atlas.", date)))
        {
            comments.Add(Comment.Create(2, 1, 1, "Segundo comentário.", date.AddDays(1)));
            comments.Add(Comment.Create(1, 1, 1, "Primeiro no empate.", date.AddDays(1)));
            comments.Add(Comment.Create(3, 1, 1, "Mais antigo.", date));
        }
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FixtureCatalog(TimeProvider clock) : IExternalGameCatalog
{
    internal static readonly Game[] Games =
    [
        new(1, "Ecos do Horizonte", "Explore os caminhos de um mundo fictício.", null, ["Aventura"], ["PC"], new(2024, 4, 12)) { IgdbId = 1 },
        new(2, "Circuito Aurora", "Corridas noturnas em circuitos de uma cidade fictícia.", null, ["Corrida"], ["PC", "Console"], new(2025, 2, 20)) { IgdbId = 2 },
        new(3, "Atlas de Cinzas", "Descubra histórias esquecidas em uma aventura de fantasia.", null, ["RPG", "Aventura"], ["PC"], null) { IgdbId = 3 }
    ];
    public Task<CatalogPage> SearchAsync(CatalogSearch query, CancellationToken cancellationToken)
    {
        IEnumerable<Game> items = Games.Where(game => game.Title.Contains(query.Term, StringComparison.OrdinalIgnoreCase));
        items = query.Sort switch
        {
            "release-desc" => items.Where(game => game.ReleaseDate <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)).OrderByDescending(game => game.ReleaseDate),
            "name-desc" => items.OrderByDescending(game => game.Title),
            _ => items.OrderBy(game => game.Title)
        };
        var metadata = new CatalogMetadata(clock.GetUtcNow());
        var page = items.Skip(query.Offset).Take(query.PageSize + 1).ToArray();
        return Task.FromResult(new CatalogPage(query, page.Take(query.PageSize).Select(game => new CatalogGame(game, metadata)).ToArray(), page.Length > query.PageSize, metadata));
    }
    public Task<IReadOnlyDictionary<long, CatalogGame>> ResolveAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<long, CatalogGame>>(Games.Where(game => ids.Contains(game.IgdbId)).ToDictionary(game => game.IgdbId,
            game => new CatalogGame(game, new(clock.GetUtcNow()))));
}
