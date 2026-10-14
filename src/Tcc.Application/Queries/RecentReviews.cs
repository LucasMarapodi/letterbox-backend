using Tcc.Application.Opinions;
using Tcc.Application.Social;

namespace Tcc.Application.Queries;
public sealed class RecentReviews(IReviews reviews, ReviewProjection projection, IPublicationScope publications)
{
    public Task<IReadOnlyList<ReviewDetails>> Execute(CancellationToken cancellationToken = default) =>
        projection.ExecuteAsync(publications.Execute(reviews.All), cancellationToken);
}
