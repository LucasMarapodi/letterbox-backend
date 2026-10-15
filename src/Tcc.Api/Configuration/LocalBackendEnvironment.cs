namespace Tcc.Api.Configuration;

public static class LocalBackendEnvironment
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    { "IGDB__ClientId", "IGDB__ClientSecret", "IGDB__TokenRefreshMarginSeconds", "ConnectionStrings__TccDatabase" };

    public static Func<string, string?> CreateReader(string path, Func<string, string?> processVariable)
    {
        var file = new Lazy<IReadOnlyDictionary<string, string>>(() => ReadFile(path));
        return name => Names.Contains(name) ? processVariable(name) ?? file.Value.GetValueOrDefault(name) : null;
    }

    public static string PathFor(string contentRoot)
    {
        var workspace = Path.GetFullPath(Path.Combine(contentRoot, "..", ".."));
        return Path.Combine(File.Exists(Path.Combine(workspace, "Tcc.slnx")) ? workspace : contentRoot, ".env");
    }

    private static IReadOnlyDictionary<string, string> ReadFile(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return values;
        try
        {
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var separator = line.IndexOf('=');
                if (separator <= 0) throw InvalidFile();
                var name = line[..separator].Trim();
                if (!Names.Contains(name)) continue;
                var value = line[(separator + 1)..].Trim();
                if (value.Length > 0 && value[0] is '\'' or '"')
                {
                    if (value.Length < 2 || value[^1] != value[0]) throw InvalidFile();
                    value = value[1..^1];
                }
                if (!values.TryAdd(name, value)) throw InvalidFile();
            }
            return values;
        }
        catch (IOException) { throw InvalidFile(); }
        catch (UnauthorizedAccessException) { throw InvalidFile(); }
    }
    private static InvalidOperationException InvalidFile() => new("Arquivo .env local inválido ou indisponível; verifique as variáveis configuradas sem expor seus valores.");
}
