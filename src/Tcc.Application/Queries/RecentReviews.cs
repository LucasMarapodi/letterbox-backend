using Tcc.Application.Opinions;
using Tcc.Application.Social;

namespace Tcc.Application.Queries;

public sealed class RecentReviews(IReviews reviews, ReviewProjection projection, IPublicationScope publications)
{
    public IReadOnlyList<ReviewDetails> Execute() => publications.Execute(() => projection.Execute(reviews.All()));
}
