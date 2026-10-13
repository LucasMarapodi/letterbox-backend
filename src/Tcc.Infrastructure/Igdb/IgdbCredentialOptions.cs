namespace Tcc.Infrastructure.Igdb;

public sealed class IgdbCredentialOptions
{
    public static IgdbCredentialOptions Read(Func<string, string?> readVariable) => throw new NotImplementedException();
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public TimeSpan RefreshMargin { get; init; } = TimeSpan.FromSeconds(30);
}
