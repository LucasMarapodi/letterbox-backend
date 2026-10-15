using Tcc.Domain.Identity;

namespace Tcc.Domain.Social;

public sealed record Follow
{
    public int FollowerId { get; }
    public int FollowedId { get; }
    public DateTimeOffset CreatedAt { get; }
    private Follow(int followerId, int followedId, DateTimeOffset createdAt) { FollowerId = followerId; FollowedId = followedId; CreatedAt = createdAt.ToUniversalTime(); }
    public static Follow Create(int followerId, int followedId, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(followerId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(followedId);
        if (followerId == followedId) throw new DomainValidationException("Não pode seguir a própria conta.");
        return new(followerId, followedId, now);
    }
}
