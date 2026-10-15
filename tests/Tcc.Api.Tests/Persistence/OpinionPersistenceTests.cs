using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcc.Api.Tests.Identity;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class OpinionPersistenceTests
{
    [Fact]
    public void Reviews_and_ratings_have_integer_identity_unique_owner_game_and_restrict_FKs()
    {
        using var db = new TccDbContext(new DbContextOptionsBuilder<TccDbContext>().UseNpgsql(BootstrapTests.SyntheticConnection).Options);
        foreach (var type in new[] { typeof(Review), typeof(Rating) })
        {
            var model = db.Model.FindEntityType(type);
            Assert.NotNull(model);
            Assert.Equal(typeof(int), Assert.Single(model.FindPrimaryKey()!.Properties).ClrType);
            Assert.Contains(model.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "UserId", "GameId" }));
            Assert.Equal(2, model.GetForeignKeys().Count());
            Assert.All(model.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
        }
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Concurrent_review_registration_edit_and_filtered_reads_survive_restart()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Writer", "writer@example.com", "synthetic-password"));
        registered.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, "writer@example.com", "synthetic-password");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { text = "Persisted review" })));
        int id;
        try
        {
            var created = Assert.Single(responses, response => (int)response.StatusCode == 201);
            Assert.Equal(7, responses.Count(response => (int)response.StatusCode == 409));
            using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            id = json.RootElement.GetProperty("id").GetInt32();
        }
        finally { foreach (var response in responses) response.Dispose(); }
        using var edited = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { text = "Edited in SQL" });
        Assert.Equal(200, (int)edited.StatusCode);
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        using var restored = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/reviews/{id}"));
        Assert.Equal("Edited in SQL", restored.RootElement.GetProperty("text").GetString());
        Assert.Equal(id, restored.RootElement.GetProperty("id").GetInt32());
        using var filtered = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/users/{login.User.Id}/reviews"));
        Assert.Equal(1, filtered.RootElement.GetArrayLength());
        Assert.Equal(1, await db.Set<Review>().CountAsync());
    }

    [PostgresFact]
    public async Task Concurrent_rating_upsert_preserves_id_and_SQL_average_distinguishes_zero_and_absence()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Rater", "rater@example.com", "synthetic-password"));
        registered.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, "rater@example.com", "synthetic-password");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        using var empty = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/games/{game.Id}"));
        Assert.Equal(JsonValueKind.Null, empty.RootElement.GetProperty("averageRating").ValueKind);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PutAsJsonAsync($"/api/v1/games/{game.Id}/rating/me", new { value = 0 })));
        var ids = new List<int>();
        try
        {
            foreach (var response in responses)
            {
                Assert.Equal(200, (int)response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                ids.Add(json.RootElement.GetProperty("id").GetInt32());
            }
        }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Single(ids.Distinct());
        await using var restarted = new PostgresApiFactory();
        using var other = restarted.CreateClient();
        using var restored = JsonDocument.Parse(await other.GetStringAsync($"/api/v1/games/{game.Id}"));
        Assert.Equal(0, restored.RootElement.GetProperty("averageRating").GetDouble());
        using var replaced = await client.PutAsJsonAsync($"/api/v1/games/{game.Id}/rating/me", new { value = 3.25 });
        replaced.EnsureSuccessStatusCode();
        using var changed = JsonDocument.Parse(await replaced.Content.ReadAsStringAsync());
        Assert.Equal(ids[0], changed.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(1, await db.Set<Rating>().CountAsync());
    }
}
