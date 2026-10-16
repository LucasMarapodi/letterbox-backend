using Tcc.Application.Identity;

namespace Tcc.Application.Opinions;

public interface IReviewLikes
{
    Task SetAsync(int userId, int reviewId, DateTimeOffset now, CancellationToken ct = default);
    Task RemoveAsync(int userId, int reviewId, CancellationToken ct = default);
    Task<bool> HasAsync(int userId, int reviewId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, int>> CountsAsync(IReadOnlyList<int> reviewIds, CancellationToken ct = default);
}

public sealed class PersonalReviewLike(GetReview reviews, IReviewLikes likes, ICurrentSession current, SessionAccess access, TimeProvider clock)
{
    private async Task<int> Require(int reviewId, CancellationToken ct)
    {
        var userId = (await access.Require(current, ct)).UserId;
        await reviews.FindAsync(reviewId, ct);
        return userId;
    }
    public async Task Set(int reviewId, CancellationToken ct = default)
    {
        var userId = await Require(reviewId, ct);
        await access.Require(current, ct);
        await likes.SetAsync(userId, reviewId, clock.GetUtcNow(), ct);
    }
    public async Task Remove(int reviewId, CancellationToken ct = default)
    {
        var userId = await Require(reviewId, ct);
        await access.Require(current, ct);
        await likes.RemoveAsync(userId, reviewId, ct);
    }
    public async Task<bool> Has(int reviewId, CancellationToken ct = default) => await likes.HasAsync(await Require(reviewId, ct), reviewId, ct);
}
