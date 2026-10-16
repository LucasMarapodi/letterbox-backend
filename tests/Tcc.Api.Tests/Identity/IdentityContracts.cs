namespace Tcc.Api.Tests.Identity;

public sealed record RegisterInput(string? Name, string? Email, string? Password);
public sealed record LoginInput(string? Email, string? Password);
public sealed record PublicUser(string Id, string Username, string DisplayName, string Bio, string? AvatarUrl);
public sealed record LoginResult(string Token, DateTimeOffset ExpiresAt, PublicUser User);
public sealed record ErrorResult(string Code, string Message);
