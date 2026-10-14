namespace Tcc.Infrastructure.Igdb;

public sealed class ExternalIntegrationUnavailableException(bool allowsStaleFallback = true)
    : Tcc.Application.Catalog.CatalogIntegrationUnavailableException(allowsStaleFallback);
