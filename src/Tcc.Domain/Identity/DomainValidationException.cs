namespace Tcc.Domain.Identity;

public sealed class DomainValidationException(string message) : Exception(message);
