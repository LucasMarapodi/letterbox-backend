using Tcc.Domain.Identity;

namespace Tcc.Application.Identity;
public interface IUserAccounts
{
    Task<UserAccount?> AddAsync(UserAccount account, CancellationToken cancellationToken = default);
    Task<UserAccount?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<UserAccount?> FindByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, UserAccount>> FindManyAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default);
    Task<UserAccount?> UpdateProfileAsync(int id, ProfileChange change, DateTimeOffset now, CancellationToken cancellationToken = default);
}
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string verifier);
}
public sealed class DatabaseUnavailableException() : Exception("Persistência temporariamente indisponível.");
