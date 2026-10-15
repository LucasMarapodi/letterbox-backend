using Tcc.Application.Opinions;
using Microsoft.Extensions.DependencyInjection;
using Tcc.Infrastructure.Opinions;
using Tcc.Domain.Opinions;
namespace Tcc.Application.Opinions;
public static class TestOpinionAccess
{
    public static IServiceCollection AddTestOpinions(this IServiceCollection services) { services.AddSingleton<IReviews, InMemoryReviews>(); services.AddSingleton<IRatings, InMemoryRatings>(); return services; }
    public static bool TryAdd(this IReviews store, Review review) => store is InMemoryReviews memory && memory.TryAdd(review);
    public static Review? Find(this IReviews store, int id) => ((InMemoryReviews)store).Find(id);
    public static IReadOnlyList<Review> All(this IReviews store) => ((InMemoryReviews)store).All();
    public static Rating? Find(this IRatings store, int userId, int gameId) => ((InMemoryRatings)store).Find(userId, gameId);
    public static Rating Set(this IRatings store, int userId, int gameId, RatingValue value) => ((InMemoryRatings)store).Set(userId, gameId, value);
}