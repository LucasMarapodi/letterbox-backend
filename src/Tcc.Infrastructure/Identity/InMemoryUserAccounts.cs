using Tcc.Application.Identity;
using Tcc.Application.Memory;
using Tcc.Domain.Identity;

namespace Tcc.Infrastructure.Identity;

public sealed class InMemoryUserAccounts : IUserAccounts
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, UserAccount> byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> idByEmail = new(StringComparer.Ordinal);

    public InMemoryUserAccounts(IHostSeed seed, IPasswordHasher passwords)
    {
        seed.SeedOnce(() => TryAdd(new UserAccount("user-1", "ana", Email.Create("ana@example.com"),
            passwords.Hash("teste123"), UserProfile.Create("Ana", "Explorando novos mundos."))));
    }

    public bool TryAdd(UserAccount account)
    {
        lock (gate)
        {
            if (idByEmail.ContainsKey(account.Email.Value) || byId.ContainsKey(account.Id) ||
                byId.Values.Any(existing => existing.Username == account.Username)) return false;
            byId.Add(account.Id, account);
            idByEmail.Add(account.Email.Value, account.Id);
            return true;
        }
    }

    public UserAccount? FindByEmail(Email email)
    {
        lock (gate) return idByEmail.TryGetValue(email.Value, out var id) ? byId[id] : null;
    }

    public UserAccount? FindById(string id)
    {
        lock (gate) return byId.GetValueOrDefault(id);
    }

    public UserAccount? UpdateProfile(string id, ProfileChange change)
    {
        lock (gate)
        {
            if (!byId.TryGetValue(id, out var account)) return null;
            var updated = account.UpdateProfile(change);
            byId[id] = updated;
            return updated;
        }
    }
}
