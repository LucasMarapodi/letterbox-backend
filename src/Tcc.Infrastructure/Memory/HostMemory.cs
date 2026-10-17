using Tcc.Application;
namespace Tcc.Infrastructure.Memory;

public sealed class HostMemory : IHostSeed
{
    private readonly Lock gate = new();
    private bool seeded;

    public void SeedOnce(Action seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        lock (gate)
        {
            if (seeded) return;
            seed();
            seeded = true;
        }
    }
}
