namespace Tcc.Application;

public sealed class DatabaseUnavailableException() : Exception("Persistência temporariamente indisponível.");
