using Tcc.Application.Identity;
using Tcc.Domain;
using Tcc.Domain.Identity;

namespace Tcc.Application.Tests;

public sealed class RegistrationPasswordTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("      ")]
    [InlineData("Abc1!")]
    [InlineData("abc1!x")]
    [InlineData("Abcd!x")]
    [InlineData("Abc12x")]
    [InlineData("Abc1 x")]
    [InlineData("Abc1\u0001x")]
    public async Task Invalid_password_never_reaches_hasher_or_repository(string? password)
    {
        var accounts = new AccountSpy();
        var hasher = new HasherSpy();
        var register = new RegisterAccount(accounts, hasher, TimeProvider.System);
        await Assert.ThrowsAsync<DomainValidationException>(() => register.Execute("User", "new@example.com", password));
        Assert.Equal(0, hasher.Calls);
        Assert.Null(accounts.Added);
    }

    [Theory]
    [InlineData("Abc1!x")]
    [InlineData("ABC1!X")]
    [InlineData(" Ab1! ")]
    [InlineData(" Abc1!longer ")]
    public async Task Valid_password_is_hashed_exactly_as_received_and_only_verifier_is_persisted(string password)
    {
        var accounts = new AccountSpy();
        var hasher = new HasherSpy();
        var user = await new RegisterAccount(accounts, hasher, TimeProvider.System).Execute("User", "new@example.com", password);
        Assert.Equal(1, hasher.Calls);
        Assert.Equal(password, hasher.Original);
        Assert.Same(accounts.Added, user);
        Assert.Equal("synthetic-verifier", user.PasswordHash);
        Assert.NotEqual(password, user.PasswordHash);
    }

    private sealed class HasherSpy : IPasswordHasher
    {
        public int Calls { get; private set; }
        public string? Original { get; private set; }
        public string Hash(string password) { Calls++; Original = password; return "synthetic-verifier"; }
        public bool Verify(string password, string verifier) => throw new NotSupportedException();
    }

    private sealed class AccountSpy : IUserAccounts
    {
        public UserAccount? Added { get; private set; }
        public Task<UserAccount?> AddAsync(UserAccount account, CancellationToken cancellationToken = default)
        { Added = account; return Task.FromResult<UserAccount?>(account); }
        public Task<UserAccount?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount?> FindByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, UserAccount>> FindManyAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount?> UpdateProfileAsync(int id, ProfileChange change, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
