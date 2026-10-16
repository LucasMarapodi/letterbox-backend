using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Domain.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Infrastructure.Persistence.Repositories;

// Only query coverage/IDs live here; descriptive game data is always reloaded from SQL.
public sealed class CatalogQuerySnapshots(CatalogMemoryOptions options)
{
    private readonly object gate = new();
    private readonly BoundedCatalogMap<CatalogKey, Snapshot> pages = new(options.SnapshotLimit);
    public void Invalidate() { lock (gate) pages.Clear(); }
    public sealed record Snapshot(CatalogPage Page, int[] Ids);
    public Snapshot? Find(CatalogSearch query, DateTimeOffset now)
    {
        lock (gate)
        {
            var key = CatalogKey.From(query);
            if (!pages.TryGet(key, out var entry)) return null;
            if (CatalogStorage.PageExpired(entry.Page.Metadata, now, options)) { pages.Remove(key); return null; }
            return entry;
        }
    }
    public void Save(CatalogPage page)
    {
        CatalogStorage.ValidatePage(page);
        lock (gate)
        {
            var key = CatalogKey.From(page.Query);
            if (!pages.TryGet(key, out var old) || old.Page.Metadata.FetchedAt <= page.Metadata.FetchedAt)
                pages.Set(key, new(page with { Items = [] }, page.Items.Select(item => item.Game.Id).ToArray()));
        }
    }
}
public sealed class PostgresGameCatalog : ILocalGameCatalog
{
    private readonly Lazy<TccDbContext> context;
    private readonly TimeProvider clock;
    private readonly CatalogMemoryOptions options;
    private readonly CatalogQuerySnapshots snapshots;
    public PostgresGameCatalog(Lazy<TccDbContext> context, TimeProvider clock, CatalogMemoryOptions options)
        : this(context, clock, options, new CatalogQuerySnapshots(options)) { }
    public PostgresGameCatalog(Lazy<TccDbContext> context, TimeProvider clock, CatalogMemoryOptions options, CatalogQuerySnapshots snapshots)
    { this.context = context; this.clock = clock; this.options = options; this.snapshots = snapshots; }
    public async Task<CatalogPage?> FindPageAsync(CatalogSearch query, CancellationToken ct)
    {
        var snapshot = snapshots.Find(query, clock.GetUtcNow());
        if (snapshot is null) { await Run(db => db.Games.AnyAsync(ct)); return null; }
        var found = await FindGamesAsync(snapshot.Ids, ct);
        if (found.Count != snapshot.Ids.Length) return null;
        return CatalogStorage.ReadPage(snapshot.Page, query, snapshot.Ids.Select(id => found[id]).ToArray(), clock.GetUtcNow(), options);
    }
    public Task<IReadOnlyDictionary<int, CatalogGame>> FindGamesAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var unique = CatalogIds.Unique(ids);
        if (unique.Length == 0) return Task.FromResult<IReadOnlyDictionary<int, CatalogGame>>(new Dictionary<int, CatalogGame>());
        return Run(db => ReadSnapshot(db, () => db.Games.AsNoTracking().Where(game => unique.Contains(game.Id)).ToArrayAsync(ct), ct));
    }
    public Task<IReadOnlyDictionary<long, CatalogGame>> FindByIgdbIdsAsync(IReadOnlyList<long> ids, CancellationToken ct)
    {
        var unique = ExternalCatalogIds.Unique(ids);
        return Run<IReadOnlyDictionary<long, CatalogGame>>(async db =>
        {
            var found = await ReadSnapshot(db, () => db.Games.AsNoTracking().Where(game => unique.Contains(game.IgdbId)).ToArrayAsync(ct), ct);
            return found.Values.ToDictionary(item => item.Game.IgdbId);
        });
    }
    public Task SavePageAsync(CatalogPage page, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); if (page.IsComplete) snapshots.Save(page); return Task.CompletedTask; }
    public async Task SaveGamesAsync(IReadOnlyList<CatalogGame> games, CancellationToken ct) => _ = await UpsertAsync(games, ct);
    public Task<IReadOnlyList<CatalogGame>> UpsertAsync(IReadOnlyList<CatalogGame> games, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        foreach (var item in games)
        {
            if (item.Game.IgdbId <= 0 || string.IsNullOrWhiteSpace(item.Game.Title)
                || item.Game.GenreEntries.Any(genre => genre.IgdbId <= 0 || string.IsNullOrWhiteSpace(genre.Name))
                || item.Game.PlatformEntries.Any(platform => platform.IgdbId <= 0 || string.IsNullOrWhiteSpace(platform.Name)))
                throw new CatalogInvalidResponseException();
        }
        if (games.Count == 0) return Task.FromResult<IReadOnlyList<CatalogGame>>([]);
        return Run<IReadOnlyList<CatalogGame>>(async db =>
        {
            var batch = games.GroupBy(item => item.Game.IgdbId).Select(group => group.OrderBy(item => item.Metadata.FetchedAt).Last()).OrderBy(item => item.Game.IgdbId).ToArray();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Short SQL-only import unit: serializes shared dimensions and association replacement across processes.
            await Scalar(db, tx, "SELECT pg_advisory_xact_lock(72422002)", ct);
            foreach (var item in batch)
            {
                var game = item.Game;
                var now = clock.GetUtcNow();
                var inserted = await Scalar(db, tx, """
                    INSERT INTO jogos (igdb_id,nome,descricao,capa_url,primeira_data_lancamento,dados_obtidos_em,criado_em,atualizado_em)
                    VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p6)
                    ON CONFLICT (igdb_id) DO UPDATE SET nome=EXCLUDED.nome,descricao=EXCLUDED.descricao,capa_url=EXCLUDED.capa_url,
                    primeira_data_lancamento=EXCLUDED.primeira_data_lancamento,dados_obtidos_em=EXCLUDED.dados_obtidos_em,atualizado_em=EXCLUDED.atualizado_em
                    WHERE jogos.dados_obtidos_em <= EXCLUDED.dados_obtidos_em RETURNING id
                    """, ct, game.IgdbId, game.Title, game.Summary, game.CoverUrl, game.ReleaseDate, item.Metadata.FetchedAt.ToUniversalTime(), now);
                if (inserted is not int gameId) continue; // A late, older response cannot replace a newer game or its relationships.
                await Scalar(db, tx, "DELETE FROM jogos_generos WHERE jogo_id=@p0", ct, gameId);
                await Scalar(db, tx, "DELETE FROM jogos_plataformas WHERE jogo_id=@p0", ct, gameId);
                foreach (var genre in game.GenreEntries.DistinctBy(entry => entry.IgdbId).OrderBy(entry => entry.IgdbId))
                {
                    var genreId = await Scalar(db, tx, "INSERT INTO generos (igdb_id,nome) VALUES (@p0,@p1) ON CONFLICT (igdb_id) DO UPDATE SET nome=EXCLUDED.nome RETURNING id", ct, genre.IgdbId, genre.Name.Trim());
                    await Scalar(db, tx, "INSERT INTO jogos_generos (jogo_id,genero_id) VALUES (@p0,@p1) ON CONFLICT DO NOTHING", ct, gameId, genreId);
                }
                foreach (var platform in game.PlatformEntries.DistinctBy(entry => entry.IgdbId).OrderBy(entry => entry.IgdbId))
                {
                    var platformId = await Scalar(db, tx, "INSERT INTO plataformas (igdb_id,nome) VALUES (@p0,@p1) ON CONFLICT (igdb_id) DO UPDATE SET nome=EXCLUDED.nome RETURNING id", ct, platform.IgdbId, platform.Name.Trim());
                    await Scalar(db, tx, "INSERT INTO jogos_plataformas (jogo_id,plataforma_id) VALUES (@p0,@p1) ON CONFLICT DO NOTHING", ct, gameId, platformId);
                }
            }
            ct.ThrowIfCancellationRequested();
            await tx.CommitAsync(ct);
            snapshots.Invalidate();
            var externalIds = batch.Select(item => item.Game.IgdbId).ToArray();
            var saved = await ReadSnapshot(db, () => db.Games.AsNoTracking().Where(game => externalIds.Contains(game.IgdbId)).ToArrayAsync(ct), ct);
            var lookup = saved.Values.ToDictionary(item => item.Game.IgdbId);
            return games.Select(item => lookup[item.Game.IgdbId]).ToArray();
        });
    }
    private async Task<IReadOnlyDictionary<int, CatalogGame>> ReadSnapshot(TccDbContext db, Func<Task<Game[]>> load, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var result = await Read(db, await load(), ct);
        await tx.CommitAsync(ct);
        return result;
    }
    private async Task<IReadOnlyDictionary<int, CatalogGame>> Read(TccDbContext db, Game[] games, CancellationToken ct)
    {
        if (games.Length == 0) return new Dictionary<int, CatalogGame>();
        var ids = games.Select(game => game.Id).ToArray();
        var genres = await db.GameGenres.AsNoTracking().Where(link => ids.Contains(link.GameId))
            .Join(db.Genres.AsNoTracking(), link => link.GenreId, genre => genre.Id, (link, genre) => new { link.GameId, Genre = genre }).ToArrayAsync(ct);
        var platforms = await db.GamePlatforms.AsNoTracking().Where(link => ids.Contains(link.GameId))
            .Join(db.Platforms.AsNoTracking(), link => link.PlatformId, platform => platform.Id, (link, platform) => new { link.GameId, Platform = platform }).ToArrayAsync(ct);
        var genreLookup = genres.ToLookup(item => item.GameId, item => item.Genre);
        var platformLookup = platforms.ToLookup(item => item.GameId, item => item.Platform);
        return games.ToDictionary(game => game.Id, game =>
        {
            var gs = genreLookup[game.Id].OrderBy(item => item.IgdbId).ToArray();
            var ps = platformLookup[game.Id].OrderBy(item => item.IgdbId).ToArray();
            var projected = game with { GenreEntries = gs, Genres = gs.Select(item => item.Name).ToArray(), PlatformEntries = ps, Platforms = ps.Select(item => item.Name).ToArray() };
            return CatalogStorage.Read(new(projected, new(game.FetchedAt)), clock.GetUtcNow(), options);
        });
    }
    private static async Task<object?> Scalar(TccDbContext db, IDbContextTransaction tx, string sql, CancellationToken ct, params object?[] values)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = tx.GetDbTransaction(); command.CommandText = sql;
        for (var i = 0; i < values.Length; i++)
        {
            var parameter = new NpgsqlParameter("p" + i, values[i] ?? DBNull.Value);
            if (values[i] is null) parameter.NpgsqlDbType = NpgsqlDbType.Unknown;
            command.Parameters.Add(parameter);
        }
        return await command.ExecuteScalarAsync(ct);
    }
    private async Task<T> Run<T>(Func<TccDbContext, Task<T>> action)
    {
        try { return await action(context.Value); }
        catch (Exception failure) when (failure is NpgsqlException or DbUpdateException or DatabaseConfigurationException
            or InvalidOperationException { InnerException: NpgsqlException }) { throw new DatabaseUnavailableException(); }
    }
}
