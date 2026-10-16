using Tcc.Application.Opinions;
using Tcc.Domain.Identity;
using Tcc.Domain.Opinions;

namespace Tcc.Application.Queries;

public sealed record RankedCommentDetails(int CommentId, int ReviewId, int GameId, int? ParentCommentId,
    string Text, DateTimeOffset CreatedAt, PublicAuthor Author, int LikeCount, int PeriodLikeCount, DateTimeOffset LastLikeAt);
public sealed record RankedCommentPage(IReadOnlyList<RankedCommentDetails> Items, int Page, int PageSize, bool HasNextPage);

public interface ICommentRanking
{
    Task<IReadOnlyList<RankedCommentDetails>> ReadAsync(DateTimeOffset from, DateTimeOffset to,
        int offset, int limit, CancellationToken cancellationToken = default);
}

public sealed class MostLikedComments(ICommentRanking ranking, TimeProvider clock)
{
    public async Task<RankedCommentPage> Execute(string? period, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var window = RankingPeriod.Parse(period);
        if (page < 1 || pageSize is not (10 or 20 or 50))
            throw new DomainValidationException("Paginação inválida.");
        var offset = ((long)page - 1) * pageSize;
        if (offset > int.MaxValue) throw new DomainValidationException("Página fora do limite permitido.");
        var now = clock.GetUtcNow();
        var rows = await ranking.ReadAsync(now - window.Duration, now, (int)offset, pageSize + 1, cancellationToken);
        return new(rows.Take(pageSize).ToArray(), page, pageSize, rows.Count > pageSize);
    }
}
