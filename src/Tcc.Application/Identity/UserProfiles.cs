using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;

public sealed class GetUserProfile(IUserAccounts accounts)
{
    public UserAccount Execute(string id) => accounts.FindById(id)
        ?? throw new IdentityFailure(IdentityFailureKind.NotFound);
}

public sealed class EditUserProfile(IUserAccounts accounts, ICurrentSession current, SessionAccess access)
{
    public UserAccount Execute(ProfileChange change)
    {
        var userId = access.Require(current).UserId;
        return accounts.UpdateProfile(userId, change) ?? throw new IdentityFailure(IdentityFailureKind.NotFound);
    }
}
