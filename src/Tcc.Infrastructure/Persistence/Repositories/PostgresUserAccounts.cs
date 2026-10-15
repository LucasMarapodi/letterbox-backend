using Tcc.Application.Identity;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresUserAccounts : IUserAccounts
{
    public Task<UserAccount?> AddAsync(UserAccount account, CancellationToken cancellationToken = default) => Task.FromResult<UserAccount?>(null);
    public Task<UserAccount?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => Task.FromResult<UserAccount?>(null);
    public Task<UserAccount?> FindByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<UserAccount?>(null);
    public Task<IReadOnlyDictionary<int, UserAccount>> FindManyAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<int, UserAccount>>(new Dictionary<int, UserAccount>());
    public Task<UserAccount?> UpdateProfileAsync(int id, ProfileChange change, DateTimeOffset now, CancellationToken cancellationToken = default) => Task.FromResult<UserAccount?>(null);
}
