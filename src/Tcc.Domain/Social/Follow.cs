using Tcc.Domain.Identity;

namespace Tcc.Domain.Social;

public sealed record Follow
{
    public int FollowerId { get; }
    public int FollowedId { get; }
    private Follow(int followerId, int followedId) { FollowerId = followerId; FollowedId = followedId; }
    public static Follow Create(int followerId, int followedId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(followerId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(followedId);
        if (followerId == followedId) throw new DomainValidationException("Não pode seguir a própria conta.");
        return new(followerId, followedId);
    }
}
