using Tcc.Domain.Identity;

namespace Tcc.Domain.Opinions;

public sealed record RankingPeriod
{
    public string Value { get; }
    public TimeSpan Duration { get; }
    private RankingPeriod(string value, int hours) { Value = value; Duration = TimeSpan.FromHours(hours); }
    public static RankingPeriod Parse(string? period) => period switch
    {
        null or "7d" => new("7d", 168),
        "24h" => new("24h", 24),
        "30d" => new("30d", 720),
        _ => throw new DomainValidationException("Período inválido.")
    };
    public bool Contains(DateTimeOffset timestamp, DateTimeOffset now) => timestamp >= now - Duration && timestamp <= now;
}
