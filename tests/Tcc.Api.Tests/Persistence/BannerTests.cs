using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Tests.Identity;
using Tcc.Application.Catalog;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class BannerTests
{
    [Fact]
    public void Mapper_prefers_horizontal_artwork_then_screenshot_and_never_stretches_cover()
    {
        var artwork = Map("""[{"image_id":"art","width":1600,"height":900}]""", """[{"image_id":"screen","width":1920,"height":1080}]""");
        Assert.EndsWith("/t_1080p/art.jpg", Banner(artwork));
        var fallback = Map("""[{"image_id":"vertical","width":600,"height":900}]""", """[{"image_id":"screen","width":1920,"height":1080}]""");
        Assert.EndsWith("/t_1080p/screen.jpg", Banner(fallback));
        Assert.Null(Banner(Map("null", "null")));
        Assert.Null(Banner(Map("[]", "[]")));
        var query = IgdbQueries.Ids([3_000_000_001]);
        Assert.Contains("artworks.width", query);
        Assert.Contains("screenshots.height", query);
    }

    [PostgresFact]
    public async Task Profile_banner_is_private_to_edit_public_to_read_durable_and_independent_of_avatar_completion()
    {
        await PostgresCatalogTests.Reset();
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        using (var anonymous = await client.PatchAsJsonAsync("/api/v1/users/me/profile", new {bannerUrl="https://example.com/banner"})) Assert.Equal(401,(int)anonymous.StatusCode);
        var owner = await NotificationPersistenceTests.Register(client,"banner-owner@example.com");
        using (var saved = await client.PatchAsJsonAsync("/api/v1/users/me/profile", new {bannerUrl="https://example.com/banner",avatarUrl="https://example.com/avatar"}))
        {
            Assert.Equal(200,(int)saved.StatusCode);
            using var json = JsonDocument.Parse(await saved.Content.ReadAsStringAsync());
            Assert.False(json.RootElement.GetProperty("completion").GetProperty("isComplete").GetBoolean());
        }
        using (var edit = await client.PatchAsJsonAsync("/api/v1/users/me/profile",new {bio="Independent edit"})) edit.EnsureSuccessStatusCode();
        foreach(var invalid in new[] {"http://example.com/banner","file:///banner","https://example.com/"+new string('x',2048)})
        { using var rejected = await client.PatchAsJsonAsync("/api/v1/users/me/profile",new {bannerUrl=invalid}); Assert.Equal(400,(int)rejected.StatusCode); }
        await NotificationPersistenceTests.Register(client,"banner-other@example.com");
        using (var own = await client.PatchAsJsonAsync("/api/v1/users/me/profile",new {bannerUrl="https://example.com/other"})) own.EnsureSuccessStatusCode();
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        using var profile = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/users/{owner.Id}"));
        Assert.Equal("https://example.com/banner",profile.RootElement.GetProperty("bannerUrl").GetString());
        Assert.Equal("https://example.com/avatar",profile.RootElement.GetProperty("avatarUrl").GetString());
        Assert.False(profile.RootElement.TryGetProperty("country",out _));
        other.DefaultRequestHeaders.Authorization = new("Bearer",owner.Token);
        using (var clear = await other.PutAsJsonAsync("/api/v1/users/me/profile-completion",new {bannerUrl=(string?)null,favoriteGameIds=Array.Empty<int>()})) clear.EnsureSuccessStatusCode();
        using var completion = JsonDocument.Parse(await other.GetStringAsync("/api/v1/users/me/profile-completion"));
        Assert.Equal(JsonValueKind.Null,completion.RootElement.GetProperty("bannerUrl").ValueKind);
        Assert.Equal("https://example.com/avatar",completion.RootElement.GetProperty("avatarUrl").GetString());
    }

    [PostgresFact]
    public async Task Featured_uses_rated_games_with_banners_and_partial_catalog_updates_preserve_images()
    {
        await PostgresCatalogTests.Reset();
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var offline = new PostgresCatalogTests.NoSupplier();
        await using var host = new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => {
            services.AddSingleton<TimeProvider>(clock); services.AddSingleton<IExternalGameCatalog>(offline); }));
        using var client = host.CreateClient();
        using (var empty = JsonDocument.Parse(await client.GetStringAsync("/api/v1/home/featured"))) Assert.Equal(JsonValueKind.Null,empty.RootElement.GetProperty("game").ValueKind);
        await using var db = PostgresTestDatabase.Context();
        var repo = PostgresCatalogTests.Repository(db);
        var games = await repo.UpsertAsync([
            Map("""[{"image_id":"art","width":1600,"height":900}]""","[]",3_000_000_001),
            Map("null","null",3_000_000_002),
            Map("[]","""[{"image_id":"screen","width":1920,"height":1080}]""",3_000_000_003)],default);
        await NotificationPersistenceTests.Register(client,"featured@example.com");
        foreach(var game in games)
        {
            clock.UtcNow=clock.UtcNow.AddMinutes(1);
            using var rated = await client.PutAsJsonAsync($"/api/v1/games/{game.Game.Id}/rating/me",new {value=game.Game.Id==games[0].Game.Id?5:3});
            rated.EnsureSuccessStatusCode();
        }
        using(var featured = JsonDocument.Parse(await client.GetStringAsync("/api/v1/home/featured")))
        {
            Assert.Equal(games[2].Game.Id,featured.RootElement.GetProperty("game").GetProperty("id").GetInt32());
            Assert.Equal("trending",featured.RootElement.GetProperty("selectionReason").GetString());
        }
        await repo.UpsertAsync([Map("null","null",3_000_000_003,clock.UtcNow)],default);
        await using var restart = new PostgresApiFactory();
        using var fresh = restart.CreateClient();
        using(var details=JsonDocument.Parse(await fresh.GetStringAsync($"/api/v1/games/{games[2].Game.Id}"))) Assert.EndsWith("/screen.jpg",details.RootElement.GetProperty("bannerUrl").GetString());
        Assert.Equal(0,offline.Calls);
        foreach(var game in games) await repo.UpsertAsync([Map("[]","[]",game.Game.IgdbId,clock.UtcNow.AddMinutes(1))],default);
        using var cleared = JsonDocument.Parse(await fresh.GetStringAsync("/api/v1/home/featured"));
        Assert.Equal(JsonValueKind.Null,cleared.RootElement.GetProperty("game").ValueKind);
        Assert.Equal("no_candidate",cleared.RootElement.GetProperty("selectionReason").GetString());
    }

    internal static CatalogGame Map(string artworks,string screenshots,long id=3_000_000_001,DateTimeOffset? fetched=null) =>
        new IgdbGameMapper(ExternalEndpointOptions.Default).Map(JsonSerializer.Deserialize<IgdbGameDto>(
            $$"""{"id":{{id}},"name":"Synthetic banner game","cover":{"image_id":"cover"},"artworks":{{artworks}},"screenshots":{{screenshots}}} """)!,fetched??DateTimeOffset.UtcNow);
    private static string? Banner(CatalogGame item)
    { using var json=JsonDocument.Parse(JsonSerializer.Serialize(item.Game,JsonSerializerOptions.Web)); return json.RootElement.GetProperty("bannerUrl").GetString(); }
}
