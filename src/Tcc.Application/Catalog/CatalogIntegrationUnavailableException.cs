namespace Tcc.Application.Catalog;

public class CatalogIntegrationUnavailableException(bool allowsStaleFallback = true) : Exception("Integração externa indisponível.")
{
    public bool AllowsStaleFallback { get; } = allowsStaleFallback;
}
