
namespace Tcc.Application.Opinions;

public sealed class RecentReviews(IReviews reviews, ReviewProjection projection)
{
    public async Task<IReadOnlyList<ReviewDetails>> Execute(CancellationToken cancellationToken = default) =>
        await projection.ExecuteAsync(await reviews.QueryAsync(new(), cancellationToken), cancellationToken);
}
