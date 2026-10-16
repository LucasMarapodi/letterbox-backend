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
        services.AddSingleton<IExternalGameCatalog, FixtureCatalog>();
        services.AddHostedService<OpinionFixtures>();
    });
}

internal sealed class OpinionFixtures(IReviews reviews, IComments comments) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var date = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        if (reviews.TryAdd(Review.Create("review-fixture", "user-1", "igdb-3", "Review fictícia de Atlas.", date)))
        {
            comments.Add(Comment.Create("comment-b", "user-1", "review-fixture", "Segundo comentário.", date.AddDays(1)));
            comments.Add(Comment.Create("comment-a", "user-1", "review-fixture", "Primeiro no empate.", date.AddDays(1)));
            comments.Add(Comment.Create("comment-c", "user-1", "review-fixture", "Mais antigo.", date));
        }
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FixtureCatalog(TimeProvider clock) : IExternalGameCatalog
{
    internal static readonly Game[] Games =
    [
        new("igdb-1", "Ecos do Horizonte", "Explore os caminhos de um mundo fictício.", null, ["Aventura"], ["PC"], new(2024, 4, 12)),
        new("igdb-2", "Circuito Aurora", "Corridas noturnas em circuitos de uma cidade fictícia.", null, ["Corrida"], ["PC", "Console"], new(2025, 2, 20)),
        new("igdb-3", "Atlas de Cinzas", "Descubra histórias esquecidas em uma aventura de fantasia.", null, ["RPG", "Aventura"], ["PC"], null)
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
    public Task<IReadOnlyDictionary<string, CatalogGame>> ResolveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, CatalogGame>>(Games.Where(game => ids.Contains(game.Id)).ToDictionary(game => game.Id,
            game => new CatalogGame(game, new(clock.GetUtcNow()))));
}
