using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Infrastructure.Memory;

namespace Tcc.Api.Tests;

public sealed class MemoryTests
{
    [Fact]
    public async Task State_and_clock_are_available_per_host()
    {
        await using var first = new WebApplicationFactory<Program>();
        await using var second = new WebApplicationFactory<Program>();
        using var client = first.CreateClient();
        using var scope = first.Services.CreateScope();
        using var otherScope = first.Services.CreateScope();
        var firstState = scope.ServiceProvider.GetService<HostMemory>();
        Assert.NotNull(firstState);
        Assert.Same(firstState, otherScope.ServiceProvider.GetRequiredService<HostMemory>());
        Assert.NotSame(firstState, second.Services.GetRequiredService<HostMemory>());
        var value = "";
        firstState.SeedOnce(() => value = "fixture");
        Assert.Equal("fixture", value);
        value = "written";
        using var response = await client.GetAsync("/health");
        Assert.Same(firstState, first.Services.GetRequiredService<HostMemory>());
        otherScope.ServiceProvider.GetRequiredService<HostMemory>().SeedOnce(() => value = "overwritten");
        Assert.Equal("written", value);
        Assert.Same(firstState, first.Services.GetRequiredService<Tcc.Application.Memory.IHostSeed>());
        Assert.Equal(TimeSpan.Zero, first.Services.GetRequiredService<TimeProvider>().GetUtcNow().Offset);
    }

    [Fact]
    public void Seed_runs_once_without_overwriting_changes_and_new_state_restores_fixture()
    {
        var state = new HostMemory();
        var value = "";
        var count = 0;
        void Seed() { value = "fixture"; count++; }
        state.SeedOnce(Seed);
        Assert.Equal("fixture", value);
        value = "written";
        Parallel.For(0, 100, _ => state.SeedOnce(Seed));
        Assert.Equal("written", value);
        Assert.Equal(1, count);
        new HostMemory().SeedOnce(Seed);
        Assert.Equal("fixture", value);
        Assert.Equal(2, count);
    }

    [Fact]
    public void Clock_can_be_replaced_for_each_host()
    {
        var instant = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        using var host = new FixedClockFactory(new FixedClock(instant));
        Assert.Equal(instant, host.Services.GetRequiredService<TimeProvider>().GetUtcNow());
    }
}

public sealed class FixedClockFactory(TimeProvider clock) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services => services.AddSingleton(clock));
}

public sealed class FixedClock(DateTimeOffset instant) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instant;
}
