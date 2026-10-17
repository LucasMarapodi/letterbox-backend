namespace Tcc.Application;

public interface IHostSeed
{
    void SeedOnce(Action seed);
}
