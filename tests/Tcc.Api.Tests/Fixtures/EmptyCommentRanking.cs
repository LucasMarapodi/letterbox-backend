using Tcc.Application.Opinions;

namespace Tcc.Api.Tests.Fixtures;

// HTTP permission/contract fixtures have no ranked comments; persistence is verified against real PostgreSQL.
internal sealed class EmptyCommentRanking : ICommentRanking
{
    public Task<IReadOnlyList<RankedCommentDetails>> ReadAsync(DateTimeOffset from, DateTimeOffset to,
        int offset, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RankedCommentDetails>>([]);
}
