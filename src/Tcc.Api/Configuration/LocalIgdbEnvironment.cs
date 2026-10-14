namespace Tcc.Api.Configuration;

public static class LocalIgdbEnvironment
{
    public static Func<string, string?> CreateReader(string path, Func<string, string?> processVariable) => processVariable;
}
