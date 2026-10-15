using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Api.Configuration;
using Tcc.Api.Tests.Identity;
using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Application.Opinions;
using Tcc.Application.Social;
using Tcc.Infrastructure.Persistence;
using Tcc.Infrastructure.Persistence.Repositories;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class FinalPersistenceFlowTests
{
    [Fact]
    public async Task Production_ports_resolve_SQL_stores_and_only_catalog_cache_is_shared_between_scopes()
    {
        await using var host = new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddPostgresPersistence(_ => BootstrapTests.SyntheticConnection)));
        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();
        var ports = new (Type Port, Type Store)[]
        {
            (typeof(IUserAccounts), typeof(PostgresUserAccounts)), (typeof(IUserSessions), typeof(PostgresUserSessions)),
            (typeof(IReviews), typeof(PostgresReviews)), (typeof(IRatings), typeof(PostgresRatings)),
            (typeof(IComments), typeof(PostgresComments)), (typeof(ICommentLikes), typeof(PostgresCommentLikes)),
            (typeof(IFavorites), typeof(PostgresFavorites)), (typeof(IFollows), typeof(PostgresFollows)),
            (typeof(INotifications), typeof(PostgresNotifications))
        };
        foreach (var (port, store) in ports)
        {
            Assert.IsType(store, first.ServiceProvider.GetRequiredService(port));
            Assert.NotSame(first.ServiceProvider.GetRequiredService(port), second.ServiceProvider.GetRequiredService(port));
        }
        Assert.Equal("Tcc.Api.Configuration.ScopedSqlLocalGameCatalog", host.Services.GetRequiredService<ILocalGameCatalog>().GetType().FullName);
        Assert.Same(first.ServiceProvider.GetRequiredService<ICatalogCache>(), second.ServiceProvider.GetRequiredService<ICatalogCache>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<TccDbContext>(), second.ServiceProvider.GetRequiredService<TccDbContext>());
        foreach (var assembly in new[] { typeof(Tcc.Domain.Identity.UserAccount).Assembly, typeof(IUserAccounts).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name!.Contains("EntityFramework", StringComparison.Ordinal) || reference.Name.Contains("Npgsql", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task Two_accounts_populate_all_tables_and_keep_content_relationships_and_session_revocation_after_API_restart()
    {
        await PostgresCatalogTests.Reset();
        int gameId, reviewId, rootId, replyId;
        (int Id, string Token) author, reader;
        string revoked;
        var offline = new PostgresCatalogTests.NoSupplier();
        var snapshots = new Dictionary<string, string>();
        await using (var original = OfflineHost(offline))
        {
            using var client = original.CreateClient();
            await using (var db = PostgresTestDatabase.Context())
                gameId = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game.Id;
            author = await NotificationPersistenceTests.Register(client, "final-author@example.com");
            using (var profile = await client.PatchAsJsonAsync("/api/v1/users/me/profile", new { displayName = "Persistent author", bio = "Final lifecycle" })) profile.EnsureSuccessStatusCode();
            reader = await NotificationPersistenceTests.Register(client, "final-reader@example.com");
            await Put(client, $"/api/v1/users/{author.Id}/follow");
            client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
            using (var rating = await client.PutAsJsonAsync($"/api/v1/games/{gameId}/rating/me", new { value = 4.5 })) rating.EnsureSuccessStatusCode();
            reviewId = await Publish(client, $"/api/v1/games/{gameId}/reviews", new { text = "Complete SQL lifecycle" });
            client.DefaultRequestHeaders.Authorization = new("Bearer", reader.Token);
            rootId = await Publish(client, $"/api/v1/reviews/{reviewId}/comments", new { text = "Persistent root" });
            replyId = await Publish(client, $"/api/v1/reviews/{reviewId}/comments", new { text = "Persistent reply", parentCommentId = rootId });
            foreach (var path in new[] { $"/api/v1/comments/{rootId}/like", $"/api/v1/comments/{replyId}/like", $"/api/v1/favorites/game/{gameId}", $"/api/v1/favorites/review/{reviewId}" }) await Put(client, path);
            revoked = (await LoginTests.Login(client, "final-author@example.com", "synthetic-password")).Token;
            client.DefaultRequestHeaders.Authorization = new("Bearer", revoked);
            using (var logout = await client.PostAsync("/api/v1/auth/logout", null)) Assert.Equal(204, (int)logout.StatusCode);
            foreach (var (key, token, path) in Reads(author, reader, gameId, reviewId, rootId))
            {
                client.DefaultRequestHeaders.Authorization = new("Bearer", token);
                snapshots[key] = await client.GetStringAsync(path);
            }
        }
        // Original host is disposed; the second host has fresh singleton caches and the same SQL target.
        await using var restarted = OfflineHost(offline);
        using var other = restarted.CreateClient();
        foreach (var (key, token, path) in Reads(author, reader, gameId, reviewId, rootId))
        {
            other.DefaultRequestHeaders.Authorization = new("Bearer", token);
            Assert.Equal(snapshots[key], await other.GetStringAsync(path));
        }
        other.DefaultRequestHeaders.Authorization = new("Bearer", revoked);
        using (var denied = await other.GetAsync("/api/v1/auth/me")) Assert.Equal(401, (int)denied.StatusCode);
        using (var comments = JsonDocument.Parse(snapshots["comments"]))
        {
            Assert.Equal(2, comments.RootElement.GetArrayLength());
            var reply = Assert.Single(comments.RootElement.EnumerateArray(), item => item.GetProperty("id").GetInt32() == replyId);
            Assert.Equal(rootId, reply.GetProperty("parentCommentId").GetInt32());
            Assert.All(comments.RootElement.EnumerateArray(), item => Assert.Equal(1, item.GetProperty("likeCount").GetInt32()));
        }
        using (var events = JsonDocument.Parse(snapshots["reader-events"])) Assert.Single(events.RootElement.EnumerateArray());
        using (var events = JsonDocument.Parse(snapshots["author-events"])) Assert.Equal(2, events.RootElement.GetArrayLength());
        using (var profile = JsonDocument.Parse(snapshots["author-me"])) Assert.Equal("Persistent author", profile.RootElement.GetProperty("displayName").GetString());
        using (var game = JsonDocument.Parse(snapshots["game"])) Assert.Equal(4.5, game.RootElement.GetProperty("averageRating").GetDouble());
        Assert.Equal(0, offline.Calls);
        await using var audit = PostgresTestDatabase.Context();
        foreach (var table in audit.Model.GetRelationalModel().Tables)
        {
            // Names come exclusively from the application's EF model, never from HTTP input.
            var count = await FinalMigrationAuditTests.Read(audit, $"SELECT count(*)::text FROM \"{table.Name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"");
            Assert.True(int.Parse(Assert.Single(count), System.Globalization.CultureInfo.InvariantCulture) > 0, $"Expected durable rows in {table.Name}.");
        }
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> OfflineHost(PostgresCatalogTests.NoSupplier supplier) =>
        new PostgresApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IExternalGameCatalog>(supplier)));

    private static IEnumerable<(string Key, string Token, string Path)> Reads((int Id, string Token) author, (int Id, string Token) reader, int game, int review, int comment)
    {
        yield return ("author-me", author.Token, "/api/v1/auth/me");
        yield return ("reader-me", reader.Token, "/api/v1/auth/me");
        yield return ("rating", author.Token, $"/api/v1/games/{game}/rating/me");
        yield return ("game", reader.Token, $"/api/v1/games/{game}");
        yield return ("reviews", reader.Token, $"/api/v1/games/{game}/reviews");
        yield return ("comments", reader.Token, $"/api/v1/reviews/{review}/comments");
        yield return ("like", reader.Token, $"/api/v1/comments/{comment}/like/me");
        yield return ("favorites", reader.Token, "/api/v1/favorites");
        yield return ("author-favorites", author.Token, "/api/v1/favorites");
        yield return ("following", reader.Token, $"/api/v1/users/{reader.Id}/following");
        yield return ("followers", reader.Token, $"/api/v1/users/{author.Id}/followers");
        yield return ("feed", reader.Token, "/api/v1/feed/following");
        yield return ("reverse-feed", author.Token, "/api/v1/feed/following");
        yield return ("reader-events", reader.Token, "/api/v1/notifications");
        yield return ("author-events", author.Token, "/api/v1/notifications");
    }
    private static async Task Put(HttpClient client, string path)
    {
        using var response = await client.PutAsync(path, null);
        response.EnsureSuccessStatusCode();
    }
    private static async Task<int> Publish(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(201, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }
}
