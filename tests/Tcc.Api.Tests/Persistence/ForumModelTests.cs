using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Tcc.Domain.Identity;

namespace Tcc.Api.Tests.Persistence;

[Collection("PostgreSQL identity")]
public sealed class ForumModelTests
{
    [Fact]
    public void Forum_has_two_independent_tables_with_integer_keys_restrict_FKs_and_bounded_text()
    {
        using var db = new Tcc.Infrastructure.Persistence.TccDbContext(new DbContextOptionsBuilder<Tcc.Infrastructure.Persistence.TccDbContext>()
            .UseNpgsql(BootstrapTests.SyntheticConnection).Options);
        foreach (var name in new[] { "ForumTopic", "ForumReply" })
        {
            var entity = db.Model.FindEntityType("Tcc.Domain.Forum." + name);
            Assert.NotNull(entity);
            Assert.Equal(typeof(int), entity.FindProperty("Id")!.ClrType);
            Assert.Equal(ValueGenerated.OnAdd, entity.FindProperty("Id")!.ValueGenerated);
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
            Assert.Equal(10000, entity.FindProperty("Text")!.GetMaxLength());
            Assert.Null(entity.FindProperty("ParentReplyId"));
            if (name == "ForumTopic") Assert.Equal(150, entity.FindProperty("Title")!.GetMaxLength());
            else Assert.Null(entity.FindProperty("GameId"));
        }
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task PostgreSQL_materializes_identity_and_relations_and_rejects_invalid_FKs_and_content()
    {
        await PostgresApiFactory.ResetAsync();
        var game = (await ProfileCompletionTests.Games())[0];
        await using var db = PostgresTestDatabase.Context();
        var now = DateTimeOffset.UtcNow;
        var author = new UserAccount(0, "", Email.Create("forum-model@example.com"), "synthetic", UserProfile.Create("Forum author", ""), now);
        db.Users.Add(author); await db.SaveChangesAsync();
        var type = typeof(UserAccount).Assembly.GetType("Tcc.Domain.Forum.ForumTopic");
        Assert.NotNull(type);
        var topic = Activator.CreateInstance(type, 0, game, author.Id, "  Discussion  ", "  Initial message  ", now)!;
        db.Add(topic); await db.SaveChangesAsync();
        var id = (int)type.GetProperty("Id")!.GetValue(topic)!;
        Assert.True(id > 0);
        db.ChangeTracker.Clear();
        var restored = await db.FindAsync(type, id);
        Assert.Equal("Discussion", type.GetProperty("Title")!.GetValue(restored));
        Assert.Equal("Initial message", type.GetProperty("Text")!.GetValue(restored));
        await using var restarted = PostgresTestDatabase.Context();
        Assert.NotNull(await restarted.FindAsync(type, id));
        var invalidFk = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO respostas_forum(topico_id,usuario_id,texto,criado_em) VALUES ({int.MaxValue},{author.Id},{"Reply"},{now})"));
        Assert.Equal("23503", invalidFk.SqlState);
        var empty = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO respostas_forum(topico_id,usuario_id,texto,criado_em) VALUES ({id},{author.Id},{"   "},{now})"));
        Assert.Equal("23514", empty.SqlState);
        Assert.Equal(0, await db.Reviews.CountAsync());
    }
}
