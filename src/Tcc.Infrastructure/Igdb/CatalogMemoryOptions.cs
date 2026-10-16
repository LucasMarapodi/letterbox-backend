namespace Tcc.Infrastructure.Igdb;

public sealed record CatalogMemoryOptions
{
    public TimeSpan QueryFreshFor { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan DetailFreshFor { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan QueryFallbackFor { get; init; } = TimeSpan.FromMinutes(60);
    public int SnapshotLimit { get; init; } = 128;
    public int CachePageLimit { get; init; } = 128;
    public int CacheGameLimit { get; init; } = 512;
    public int InFlightLimit { get; init; } = 128;
    public void Validate()
    {
        if (QueryFreshFor <= TimeSpan.Zero || QueryFreshFor > TimeSpan.FromHours(1) || DetailFreshFor <= TimeSpan.Zero
            || DetailFreshFor > TimeSpan.FromDays(1) || QueryFallbackFor < TimeSpan.Zero
            || QueryFallbackFor > TimeSpan.FromHours(1) || SnapshotLimit is < 1 or > 10000 || CachePageLimit is < 1 or > 10000
            || CacheGameLimit is < 1 or > 10000 || InFlightLimit is < 1 or > 1000)
            throw new InvalidOperationException("Configuração de memória do catálogo inválida.");
    }
}
