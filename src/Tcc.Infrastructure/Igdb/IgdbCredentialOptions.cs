using System.Globalization;
using System.Text.Json.Serialization;

namespace Tcc.Infrastructure.Igdb;

public sealed class IgdbCredentialOptions
{
    private IgdbCredentialOptions(string clientId, string clientSecret, TimeSpan refreshMargin)
    {
        ClientId = clientId;
        ClientSecret = clientSecret;
        RefreshMargin = refreshMargin;
    }

    public static IgdbCredentialOptions Read(Func<string, string?> readVariable)
    {
        var id = readVariable("IGDB__ClientId");
        var secret = readVariable("IGDB__ClientSecret");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret)
            || id.Any(char.IsControl) || secret.Any(char.IsControl))
            throw new InvalidOperationException("Configure IGDB__ClientId e IGDB__ClientSecret nas variáveis do processo backend.");
        var rawMargin = readVariable("IGDB__TokenRefreshMarginSeconds");
        var margin = 30;
        if (rawMargin is not null && (!int.TryParse(rawMargin, NumberStyles.None, CultureInfo.InvariantCulture, out margin) || margin < 0 || margin > 3600))
            throw new InvalidOperationException("IGDB__TokenRefreshMarginSeconds deve ser inteiro de 0 a 3600.");
        return new(id, secret, TimeSpan.FromSeconds(margin));
    }

    [JsonIgnore] public string ClientId { get; }
    [JsonIgnore] public string ClientSecret { get; }
    public TimeSpan RefreshMargin { get; }
    public override string ToString() => "IGDB credentials (redacted)";
}
