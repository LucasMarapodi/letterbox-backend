using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;

public interface IUserAccounts
{
    bool TryAdd(UserAccount account);
    UserAccount? FindByEmail(Email email);
    UserAccount? FindById(string id);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string verifier);
}
