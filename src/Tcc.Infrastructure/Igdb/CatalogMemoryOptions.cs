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
}
