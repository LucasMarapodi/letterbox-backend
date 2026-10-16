using Microsoft.EntityFrameworkCore;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

public sealed class CatalogModelTests
{
    [Fact]
    public void Catalog_entities_have_local_integer_keys_external_bigint_and_unique_pairs()
    {
        using var db = new TccDbContext(new DbContextOptionsBuilder<TccDbContext>().UseNpgsql(BootstrapTests.SyntheticConnection).Options);
        foreach (var name in new[] { "Game", "Genre", "Platform", "GameGenre", "GamePlatform" })
        {
            var entity = db.Model.FindEntityType("Tcc.Domain.Catalog." + name);
            Assert.NotNull(entity);
            Assert.All(entity.FindPrimaryKey()!.Properties, property => Assert.Equal(typeof(int), property.ClrType));
            Assert.All(entity.GetForeignKeys(), relation => Assert.Equal(DeleteBehavior.Restrict, relation.DeleteBehavior));
            if (name is "GameGenre" or "GamePlatform") Assert.Equal(2, entity.FindPrimaryKey()!.Properties.Count);
            else
            {
                Assert.Equal(typeof(long), entity.FindProperty("IgdbId")?.ClrType);
                Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == "IgdbId");
            }
        }
        Assert.Equal(8, db.Model.GetEntityTypes().Count());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
