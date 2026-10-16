namespace Tcc.Application.Memory;

public interface IHostSeed
{
    void SeedOnce(Action seed);
}
