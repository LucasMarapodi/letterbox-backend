using Tcc.Domain.Identity;
using Tcc.Domain.Opinions;

namespace Tcc.Domain.Tests;

public sealed class RankingPeriodTests
{
    [Theory]
    [InlineData("24h", 24)]
    [InlineData("7d", 168)]
    [InlineData("30d", 720)]
    public void Period_uses_exact_hours_with_inclusive_limits_and_excludes_future(string value, int hours)
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        var period = RankingPeriod.Parse(value);
        Assert.Equal(TimeSpan.FromHours(hours), period.Duration);
        Assert.True(period.Contains(now.AddHours(-hours), now));
        Assert.True(period.Contains(now, now));
        Assert.False(period.Contains(now.AddHours(-hours).AddTicks(-1), now));
        Assert.False(period.Contains(now.AddTicks(1), now));
        Assert.Equal(period.Contains(now, now), period.Contains(now.ToOffset(TimeSpan.FromHours(-3)), now));
    }
    [Theory]
    [InlineData("")]
    [InlineData("24H")]
    [InlineData("other")]
    public void Invalid_period_is_a_domain_validation_failure(string period) =>
        Assert.Throws<DomainValidationException>(() => RankingPeriod.Parse(period));
    [Fact]
    public void Omitted_period_is_seven_days() => Assert.Equal(RankingPeriod.Parse("7d"), RankingPeriod.Parse(null));
}
