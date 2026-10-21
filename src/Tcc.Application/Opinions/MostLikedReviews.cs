using Tcc.Domain;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Opinions;

public sealed record LikedReviewDetails(ReviewDetails Review, int PeriodLikeCount, DateTimeOffset LastLikeAt);
public sealed record LikedReviewPage(IReadOnlyList<LikedReviewDetails> Items, int Page, int PageSize, bool HasNextPage);
public interface IReviewRanking
{
    Task<IReadOnlyList<LikedReviewDetails>> ReadAsync(DateTimeOffset from, DateTimeOffset to,
        int offset, int limit, CancellationToken cancellationToken = default);
}
public sealed class MostLikedReviews(IReviewRanking ranking, TimeProvider clock)
{
    public async Task<LikedReviewPage> Execute(string? period, int page, int pageSize, CancellationToken ct = default)
    {
        var window = RankingPeriod.Parse(period);
        if (page < 1 || pageSize is not (10 or 20 or 50) || ((long)page - 1) * pageSize > int.MaxValue)
            throw new DomainValidationException("Paginação inválida.");
        var now = clock.GetUtcNow();
        var rows = await ranking.ReadAsync(now - window.Duration, now, (page - 1) * pageSize, pageSize + 1, ct);
        return new(rows.Take(pageSize).ToArray(), page, pageSize, rows.Count > pageSize);
    }
}
