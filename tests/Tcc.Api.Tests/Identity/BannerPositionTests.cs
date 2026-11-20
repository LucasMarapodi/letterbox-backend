using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Tcc.Api.Tests.Identity;

public sealed class BannerPositionTests
{
    [Fact]
    public async Task Position_is_public_preserved_by_other_edits_and_reset_on_removal()
    {
        await using var host = new TestApiFactory();
        using var client = host.CreateClient();
        var login = await LoginTests.Login(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        using var saved = await client.PatchAsJsonAsync("/api/v1/users/me/profile", new { bannerUrl="https://example.test/banner", bannerPositionX=20, bannerPositionY=80 });
        Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        using var changed = await client.PatchAsJsonAsync("/api/v1/users/me/profile",new {bio="Only bio"});
        changed.EnsureSuccessStatusCode();
        using var profile=JsonDocument.Parse(await client.GetStringAsync("/api/v1/users/1"));
        Assert.Equal(20,profile.RootElement.GetProperty("bannerPositionX").GetInt32());
        Assert.Equal(80,profile.RootElement.GetProperty("bannerPositionY").GetInt32());
        using var completion=JsonDocument.Parse(await client.GetStringAsync("/api/v1/users/me/profile-completion"));
        Assert.Equal(80,completion.RootElement.GetProperty("bannerPositionY").GetInt32());
        using var clear=await client.PatchAsJsonAsync("/api/v1/users/me/profile",new {bannerUrl=(string?)null});
        clear.EnsureSuccessStatusCode();
        using var cleared=JsonDocument.Parse(await clear.Content.ReadAsStringAsync());
        Assert.Equal(50,cleared.RootElement.GetProperty("bannerPositionX").GetInt32());
        Assert.Equal(50,cleared.RootElement.GetProperty("bannerPositionY").GetInt32());
    }

    [Theory]
    [InlineData(-1,50)]
    [InlineData(50,101)]
    public async Task Invalid_position_is_rejected(int x,int y)
    {
        await using var host = new TestApiFactory();using var client=host.CreateClient();
        var login=await LoginTests.Login(client);client.DefaultRequestHeaders.Authorization=new("Bearer",login.Token);
        using var response=await client.PatchAsJsonAsync("/api/v1/users/me/profile",new {bannerPositionX=x,bannerPositionY=y});
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
    }
}
