using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Tests.Identity;
using Tcc.Domain.Catalog;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class GameDiscoveryTests
{
    [PostgresFact]
    public async Task Rankings_use_local_ratings_and_only_actual_rating_changes_update_activity()
    {
        await PostgresCatalogTests.Reset();
        var ids = await Seed();
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var host = new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var client = host.CreateClient();
        var a = await NotificationPersistenceTests.Register(client, "discovery-a@example.com");
        var b = await NotificationPersistenceTests.Register(client, "discovery-b@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", a.Token);
        await Rate(client, ids[0], 0);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        client.DefaultRequestHeaders.Authorization = new("Bearer", b.Token);
        await Rate(client, ids[0], 4);
        client.DefaultRequestHeaders.Authorization = new("Bearer", a.Token);
        for (var i = 1; i <= 3; i++) { clock.UtcNow = clock.UtcNow.AddMinutes(1); await Rate(client, ids[i], i == 3 ? 0 : 5); }
        using var first = await Read(client, "/api/v1/games/top-rated?pageSize=2");
        Assert.Equal(new[] { ids[2], ids[1] }, GameIds(first));
        Assert.True(first.RootElement.GetProperty("hasNextPage").GetBoolean());
        using var second = await Read(client, "/api/v1/games/top-rated?pageSize=2&page=2");
        Assert.Equal(new[] { ids[0], ids[3] }, GameIds(second));
        Assert.Equal(2, second.RootElement.GetProperty("items")[0].GetProperty("ratingCount").GetInt32());
        Assert.Equal(2, second.RootElement.GetProperty("items")[0].GetProperty("averageRating").GetDouble());
        Assert.Equal(0, second.RootElement.GetProperty("items")[1].GetProperty("averageRating").GetDouble());
        await using var db = PostgresTestDatabase.Context();
        var reviewId = await db.Reviews.Where(review => review.UserId == a.Id && review.GameId == ids[1]).Select(review => review.Id).SingleAsync();
        var original = await db.Ratings.AsNoTracking().SingleAsync(rating => rating.UserId == a.Id && rating.GameId == ids[1]);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        using (var edit = await client.PatchAsJsonAsync($"/api/v1/reviews/{reviewId}", new { title = "New title", text = "Only text" })) edit.EnsureSuccessStatusCode();
        await Rate(client, ids[1], 5);
        Assert.Equal(original.UpdatedAt, (await db.Ratings.AsNoTracking().SingleAsync(rating => rating.Id == original.Id)).UpdatedAt);
        using (var trending = await Read(client, "/api/v1/games/trending")) Assert.Equal(ids[3], GameIds(trending)[0]);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await Rate(client, ids[1], 4);
        using var changed = await Read(client, "/api/v1/games/trending");
        Assert.Equal(ids[1], GameIds(changed)[0]);
        Assert.Equal(4, GameIds(changed).Length);
    }

    [PostgresFact]
    public async Task Recommendations_are_private_distinct_paginated_and_recomputed_from_current_preferences()
    {
        await PostgresCatalogTests.Reset();
        var ids = await Seed();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var a = await NotificationPersistenceTests.Register(client, "recommend-a@example.com");
        var b = await NotificationPersistenceTests.Register(client, "recommend-b@example.com");
        await Favorite(client, ids[1]);
        client.DefaultRequestHeaders.Authorization = new("Bearer", a.Token);
        await Favorite(client, ids[0]); await Favorite(client, ids[6]);
        await Rate(client, ids[5], 4);
        using var first = await Read(client, "/api/v1/games/recommendations/me?pageSize=1");
        Assert.Equal(ids[2], Assert.Single(GameIds(first)));
        Assert.Equal(2, first.RootElement.GetProperty("items")[0].GetProperty("score").GetInt32());
        Assert.True(first.RootElement.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal("Fantasy", first.RootElement.GetProperty("items")[0].GetProperty("matchedThemes")[0].GetString());
        using (var next = await Read(client, "/api/v1/games/recommendations/me?pageSize=1&page=2")) Assert.Equal(ids[3], Assert.Single(GameIds(next)));
        client.DefaultRequestHeaders.Authorization = new("Bearer", b.Token);
        using (var other = await Read(client, "/api/v1/games/recommendations/me")) Assert.Equal(ids[4], Assert.Single(GameIds(other)));
        client.DefaultRequestHeaders.Authorization = new("Bearer", a.Token);
        await Rate(client, ids[2], 3); await Rate(client, ids[3], 3);
        using (var empty = await Read(client, "/api/v1/games/recommendations/me")) Assert.Equal("no_matches", empty.RootElement.GetProperty("reason").GetString());
        foreach(var id in new[] { ids[0], ids[6] }) { using var removed = await client.DeleteAsync($"/api/v1/favorites/game/{id}"); removed.EnsureSuccessStatusCode(); }
        using var absent = await Read(client, "/api/v1/games/recommendations/me");
        Assert.Empty(GameIds(absent));
        Assert.Equal("no_preferences", absent.RootElement.GetProperty("reason").GetString());
        Assert.True(absent.RootElement.TryGetProperty("items", out _));
    }

    [PostgresFact]
    public async Task Discovery_contract_rejects_anonymous_personal_reads_and_invalid_pages()
    {
        await PostgresCatalogTests.Reset();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        using (var anonymous = await client.GetAsync("/api/v1/games/recommendations/me")) Assert.Equal(401, (int)anonymous.StatusCode);
        foreach (var path in new[] { "top-rated", "trending" })
        {
            using var empty = await Read(client, "/api/v1/games/" + path);
            Assert.Empty(GameIds(empty));
            foreach(var query in new[] { "page=0", "pageSize=51", "pageSize=0", "page=2147483647&pageSize=50" })
            {
                using var invalid = await client.GetAsync($"/api/v1/games/{path}?{query}");
                Assert.Equal(400, (int)invalid.StatusCode);
            }
        }
    }

    private static async Task<int[]> Seed()
    {
        await using var db = PostgresTestDatabase.Context();
        var games = Enumerable.Range(0,8).Select(index =>
        {
            var item = PostgresCatalogTests.Payload(3_000_000_100 + index);
            var other = index is 1 or 4;
            var genre = other ? new Genre(0,13,"Adventure") : new Genre(0,12,"RPG");
            var theme = other ? new Theme(0,41,"Science fiction") : new Theme(0,40,"Fantasy");
            return item with { Game = item.Game with {
                GenreEntries = index == 7 ? [] : [genre], ThemesFetchedAt = item.Metadata.FetchedAt,
                ThemeEntries = index is 3 or 7 ? [] : [theme] } };
        }).ToArray();
        return (await PostgresCatalogTests.Repository(db).UpsertAsync(games, default)).Select(item => item.Game.Id).ToArray();
    }
    private static async Task Rate(HttpClient client, int id, double value) { using var response = await client.PutAsJsonAsync($"/api/v1/games/{id}/rating/me", new {value}); response.EnsureSuccessStatusCode(); }
    private static async Task Favorite(HttpClient client,int id) { using var response = await client.PutAsync($"/api/v1/favorites/game/{id}",null); response.EnsureSuccessStatusCode(); }
    private static async Task<JsonDocument> Read(HttpClient client,string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(200,(int)response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
    private static int[] GameIds(JsonDocument page) => page.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("game").GetProperty("id").GetInt32()).ToArray();
}
