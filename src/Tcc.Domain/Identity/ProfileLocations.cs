namespace Tcc.Domain.Identity;

public static class ProfileLocations
{
    private static readonly IReadOnlyDictionary<string, string[]> States = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Brasil"] = ["São Paulo", "Rio de Janeiro", "Minas Gerais"],
        ["Estados Unidos"] = ["Califórnia", "Nova York"],
        ["China"] = ["Taiwan", "Hubei"]
    };
    public static void Validate(string? country, string? state)
    {
        if (country is null && state is null) return;
        if (country is null || !States.TryGetValue(country, out var options) || state is not null && !options.Contains(state, StringComparer.Ordinal))
            throw new DomainValidationException("Selecione país e estado compatíveis entre as opções disponíveis.");
    }
}
