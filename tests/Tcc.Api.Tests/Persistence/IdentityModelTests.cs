using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Tcc.Domain.Identity;
using Tcc.Infrastructure.Persistence;

namespace Tcc.Api.Tests.Persistence;

public sealed class IdentityModelTests
{
    [Fact]
    public void User_and_owned_profile_have_integer_identity_private_columns_and_unique_indexes()
    {
        using var context = new TccDbContext(new DbContextOptionsBuilder<TccDbContext>().UseNpgsql(BootstrapTests.SyntheticConnection).Options);
        var user = context.Model.FindEntityType(typeof(UserAccount));
        Assert.NotNull(user);
        Assert.Equal("usuarios", user.GetTableName());
        var id = user.FindProperty("Id");
        Assert.NotNull(id);
        Assert.Equal(typeof(int), id.ClrType);
        Assert.Equal(ValueGenerated.OnAdd, id.ValueGenerated);
        Assert.NotNull(user.FindProperty("CreatedAt"));
        Assert.NotNull(user.FindProperty("UpdatedAt"));
        Assert.Contains(user.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == "Email");
        Assert.Contains(user.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == "Username");
        var profile = context.Model.FindEntityType(typeof(UserProfile));
        Assert.NotNull(profile);
        Assert.True(profile.IsOwned());
        Assert.Equal("perfis_usuarios", profile.GetTableName());
        Assert.Equal(typeof(int), Assert.Single(profile.FindPrimaryKey()?.Properties ?? []).ClrType);
        Assert.NotNull(profile.FindProperty("UpdatedAt"));
        Assert.Equal(12, context.Model.GetEntityTypes().Count());
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
