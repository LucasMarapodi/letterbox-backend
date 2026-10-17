using Tcc.Application.Identity;
using Tcc.Domain;

namespace Tcc.Application.Opinions;

public interface ICommentLikes
{
    Task SetAsync(int userId, int commentId, DateTimeOffset now, CancellationToken ct = default);
    Task RemoveAsync(int userId, int commentId, CancellationToken ct = default);
    Task<bool> HasAsync(int userId, int commentId, CancellationToken ct = default);
}
public sealed class PersonalCommentLike(IComments comments, ICommentLikes likes, ICurrentSession current, SessionAccess access, TimeProvider clock)
{
    private async Task<int> Require(int commentId, CancellationToken ct)
    {
        var userId = (await access.Require(current, ct)).UserId;
        if (commentId <= 0) throw new DomainValidationException("ID de comentário inválido.");
        if (await comments.FindAsync(commentId, ct) is null) throw new ResourceFailure(ResourceFailureKind.NotFound);
        return userId;
    }
    public async Task Set(int commentId, CancellationToken ct = default)
    {
        var userId = await Require(commentId, ct);
        await access.Require(current, ct);
        await likes.SetAsync(userId, commentId, clock.GetUtcNow(), ct);
    }
    public async Task Remove(int commentId, CancellationToken ct = default)
    {
        var userId = await Require(commentId, ct);
        await access.Require(current, ct);
        await likes.RemoveAsync(userId, commentId, ct);
    }
    public async Task<bool> Has(int commentId, CancellationToken ct = default) => await likes.HasAsync(await Require(commentId, ct), commentId, ct);
}
