using Microsoft.EntityFrameworkCore;
using Tcc.Application.Catalog;
using Tcc.Domain.Catalog;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed partial class PostgresGameCatalog
{
    public Task<CatalogFilterOptions> ReadAsync(CancellationToken ct) => Run(async db => new CatalogFilterOptions(
        await db.Genres.AsNoTracking().OrderBy(item => item.Name).ThenBy(item => item.Id).Select(item => new CatalogFilterOption(item.Id, item.Name)).ToArrayAsync(ct),
        await db.Platforms.AsNoTracking().OrderBy(item => item.Name).ThenBy(item => item.Id).Select(item => new CatalogFilterOption(item.Id, item.Name)).ToArrayAsync(ct)));

    public Task SynchronizeAsync(ExternalCatalogFilters filters, CancellationToken ct) => Run(async db =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Scalar(db, tx, "SELECT pg_advisory_xact_lock(72422002)", ct);
        foreach (var item in filters.Genres.OrderBy(item => item.Id))
            await Scalar(db, tx, "INSERT INTO generos(igdb_id,nome) VALUES(@p0,@p1) ON CONFLICT(igdb_id) DO UPDATE SET nome=EXCLUDED.nome", ct, item.Id, item.Name);
        foreach (var item in filters.Platforms.OrderBy(item => item.Id))
            await Scalar(db, tx, "INSERT INTO plataformas(igdb_id,nome) VALUES(@p0,@p1) ON CONFLICT(igdb_id) DO UPDATE SET nome=EXCLUDED.nome", ct, item.Id, item.Name);
        await tx.CommitAsync(ct);
        snapshots.Invalidate();
        return true;
    });

    public Task<CatalogFilterBindings> ResolveAsync(int? genreId, int? platformId, CancellationToken ct) => Run(async db =>
    {
        long? genre = null, platform = null;
        if (genreId.HasValue)
        {
            genre = await db.Genres.AsNoTracking().Where(item => item.Id == genreId.Value).Select(item => (long?)item.IgdbId).SingleOrDefaultAsync(ct);
            if (genre is null) throw new InvalidCatalogFilterException();
        }
        if (platformId.HasValue)
        {
            platform = await db.Platforms.AsNoTracking().Where(item => item.Id == platformId.Value).Select(item => (long?)item.IgdbId).SingleOrDefaultAsync(ct);
            if (platform is null) throw new InvalidCatalogFilterException();
        }
        return new CatalogFilterBindings(genre, platform);
    });

    public async Task<CatalogPage> ImportReleasePageAsync(CatalogSearch query, IReadOnlyList<CatalogGame> window, DateTimeOffset now, CancellationToken ct, int? windowOffset = null)
    {
        var imported = await UpsertAsync(window, ct);
        var ids = imported.Select(item => item.Game.Id).ToArray();
        var found = await Run(db => ReadSnapshot(db, () => Order(ApplyFilters(db,
            db.Games.AsNoTracking().Where(game => ids.Contains(game.Id)), query, now), query)
            .Skip(windowOffset ?? query.Offset).Take(query.PageSize + 1).ToArrayAsync(ct), ct));
        var items = found.Values.ToArray();
        return new CatalogPage(query, items.Take(query.PageSize).ToArray(), items.Length > query.PageSize,
            new CatalogMetadata(imported.Select(item => item.Metadata.FetchedAt).Append(now).Min()), AlreadyPersisted: true);
    }

    private static IQueryable<Game> ApplyFilters(TccDbContext db, IQueryable<Game> games, CatalogSearch query, DateTimeOffset now)
    {
        if (query.GenreId.HasValue) games = games.Where(game => db.GameGenres.Any(link => link.GameId == game.Id && link.GenreId == query.GenreId.Value));
        if (query.PlatformId.HasValue) games = games.Where(game => db.GamePlatforms.Any(link => link.GameId == game.Id && link.PlatformId == query.PlatformId.Value));
        if (query.Term.Length > 0)
        {
            var term = query.Term.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            games = games.Where(game => EF.Functions.ILike(game.Title, "%" + term + "%", "\\"));
        }
        if (query.Term.Length == 0 || query.Sort == "release-desc")
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            games = games.Where(game => game.ReleaseDate != null && game.ReleaseDate <= today);
        }
        return games;
    }
    private static IOrderedQueryable<Game> Order(IQueryable<Game> games, CatalogSearch query) => (query.Sort switch
    {
        "name-asc" => games.OrderBy(game => game.Title),
        "name-desc" => games.OrderByDescending(game => game.Title),
        _ => games.OrderByDescending(game => game.ReleaseDate)
    }).ThenBy(game => game.Id);
}
