using Tcc.Domain.Identity;

namespace Tcc.Domain.Social;

public sealed record Follow
{
    public string FollowerId { get; }
    public string FollowedId { get; }
    private Follow(string followerId, string followedId) { FollowerId = followerId; FollowedId = followedId; }
    public static Follow Create(string followerId, string followedId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(followerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(followedId);
        if (followerId == followedId) throw new DomainValidationException("Não pode seguir a própria conta.");
        return new(followerId, followedId);
    }
}
