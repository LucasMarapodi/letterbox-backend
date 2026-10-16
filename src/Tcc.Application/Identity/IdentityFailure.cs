namespace Tcc.Application.Identity;

public enum IdentityFailureKind { Conflict, Unauthorized, NotFound }

public sealed class IdentityFailure(IdentityFailureKind kind) : Exception("Operação de identidade rejeitada.")
{
    public IdentityFailureKind Kind { get; } = kind;
}
