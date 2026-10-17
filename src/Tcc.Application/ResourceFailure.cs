namespace Tcc.Application;

public enum ResourceFailureKind { NotFound, Conflict, Forbidden }
public sealed class ResourceFailure(ResourceFailureKind kind) : Exception("Operação rejeitada.")
{
    public ResourceFailureKind Kind { get; } = kind;
}
