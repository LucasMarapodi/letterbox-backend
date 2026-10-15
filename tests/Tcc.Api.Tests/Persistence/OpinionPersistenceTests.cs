using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Application.Social;
using Tcc.Application.Opinions;
using Tcc.Api.Tests.Social;
using Tcc.Infrastructure.Social;
using Npgsql;
using Tcc.Api.Tests.Identity;
using Tcc.Domain.Opinions;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class OpinionPersistenceTests
{
    [PostgresFact]
    public async Task Database_enforces_rating_range_review_text_and_foreign_keys()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Writer", "constraints@example.com", "synthetic-password"));
        registered.EnsureSuccessStatusCode();
        using var user = JsonDocument.Parse(await registered.Content.ReadAsStringAsync());
        var userId = user.RootElement.GetProperty("id").GetInt32();
        foreach (var value in new[] { -0.1, 5.1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO avaliacoes(usuario_id,jogo_id,nota,criado_em,atualizado_em) VALUES({userId},{game.Id},{value},now(),now())"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        }
        var textFailure = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO reviews(usuario_id,jogo_id,texto,criado_em) VALUES({userId},{game.Id},{"   "},now())"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, textFailure.SqlState);
        var missingUser = int.MaxValue;
        var fkFailure = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO reviews(usuario_id,jogo_id,texto,criado_em) VALUES({missingUser},{game.Id},{"Invalid FK"},now())"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, fkFailure.SqlState);
        Assert.Equal(0, await db.Reviews.CountAsync());
        Assert.Equal(0, await db.Ratings.CountAsync());
    }

    [PostgresFact]
    public async Task Notification_preparation_failure_rolls_back_SQL_review_and_pending_events()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        await using var original = new PostgresApiFactory();
        await using var host = original.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped<INotifications>(provider => new FailingNotifications(new Tcc.Infrastructure.Persistence.Repositories.PostgresNotifications(provider.GetRequiredService<Lazy<TccDbContext>>())))));
        using var client = host.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Writer", "rollback@example.com", "synthetic-password"));
        registered.EnsureSuccessStatusCode();
        var login = await LoginTests.Login(client, "rollback@example.com", "synthetic-password");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        using var failed = await client.PostAsJsonAsync($"/api/v1/games/{game.Id}/reviews", new { text = "Must roll back" });
        Assert.Equal(500, (int)failed.StatusCode);
        Assert.Equal(0, await db.Reviews.CountAsync());
        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [PostgresFact]
    public async Task SQL_averages_group_only_requested_games_and_preserve_rating_identity_and_creation()
    {
        await PostgresApiFactory.ResetAsync();
        await using var db = PostgresTestDatabase.Context();
        var game = Assert.Single(await PostgresCatalogTests.Repository(db).UpsertAsync([PostgresCatalogTests.Payload()], default)).Game;
        await using var host = new PostgresApiFactory();
        using var client = host.CreateClient();
        var users = new List<int>();
        foreach (var email in new[] { "average-a@example.com", "average-b@example.com" })
        {
            using var result = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Rater", email, "synthetic-password"));
            result.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
            users.Add(body.RootElement.GetProperty("id").GetInt32());
        }
        using var scope = host.Services.CreateScope();
        var ratings = scope.ServiceProvider.GetRequiredService<IRatings>();
        var now = DateTimeOffset.UtcNow;
        var first = await ratings.SetAsync(users[0], game.Id, RatingValue.Create(0), now);
        await ratings.SetAsync(users[1], game.Id, RatingValue.Create(5), now);
        var averages = await ratings.AveragesAsync([game.Id, int.MaxValue]);
        Assert.Equal(2.5, averages[game.Id]);
        Assert.False(averages.ContainsKey(int.MaxValue));
        var edited = await ratings.SetAsync(users[0], game.Id, RatingValue.Create(3), now.AddMinutes(1));
        Assert.Equal(first.Id, edited.Id);
        Assert.Equal(first.CreatedAt, edited.CreatedAt);
        Assert.True(edited.UpdatedAt > first.UpdatedAt);
        Assert.Equal(4, (await ratings.AveragesAsync([game.Id]))[game.Id]);
    }

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
        using var invalid = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { text = "   " });
        Assert.Equal(400, (int)invalid.StatusCode);
        using var outsider = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterInput("Other", "other@example.com", "synthetic-password"));
        outsider.EnsureSuccessStatusCode();
        var otherLogin = await LoginTests.Login(client, "other@example.com", "synthetic-password");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherLogin.Token);
        using var forbidden = await client.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { text = "Unauthorized edit" });
        Assert.Equal(403, (int)forbidden.StatusCode);
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
