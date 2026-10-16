namespace Tcc.Infrastructure.Igdb;

public sealed class ExternalEndpointOptions
{
    private ExternalEndpointOptions(string baseUrl, string imagesBaseUrl, string tokenEndpoint)
    {
        BaseUrl = Parse(baseUrl, directory: true);
        ImagesBaseUrl = Parse(imagesBaseUrl, directory: true);
        TokenEndpoint = Parse(tokenEndpoint, directory: false);
    }

    public Uri BaseUrl { get; }
    public Uri ImagesBaseUrl { get; }
    public Uri TokenEndpoint { get; }

    public static ExternalEndpointOptions Default { get; } = Read(_ => null);

    public static ExternalEndpointOptions Read(Func<string, string?> read) => new(
        read("IGDB__BaseUrl") ?? "https://api.igdb.com/v4/",
        read("IGDB__ImagesBaseUrl") ?? "https://images.igdb.com/igdb/image/upload/",
        read("Twitch__TokenEndpoint") ?? "https://id.twitch.tv/oauth2/token");

    private static Uri Parse(string value, bool directory)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsWellFormedOriginalString() || string.IsNullOrEmpty(uri.Host)
            || uri.UserInfo.Length != 0 || value.Contains('?') || value.Contains('#') || value.Contains('\\'))
            throw new InvalidOperationException("Configuração de URLs externas inválida; use URLs absolutas HTTPS sem credenciais, query ou fragmento.");
        return directory ? new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute) : uri;
    }

    public override string ToString() => "External endpoints (validated)";
}
