using Microsoft.AspNetCore.Diagnostics;
using Tcc.Api.Contracts;
using Tcc.Application.Catalog;
using Tcc.Application.Identity;
using Tcc.Application;
using Tcc.Domain.Opinions;
using Tcc.Domain;

namespace Tcc.Api.ErrorHandling;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            DomainValidationException => 400,
            InvalidCatalogFilterException => 400,
            CatalogIntegrationUnavailableException => 503,
            CatalogInvalidResponseException => 502,
            CatalogTimeoutException => 504,
            DatabaseUnavailableException => 503,
            ReviewOwnershipException => 403,
            ReviewTitleException => 400,
            ResourceFailure { Kind: ResourceFailureKind.NotFound } => 404,
            ResourceFailure { Kind: ResourceFailureKind.Conflict } => 409,
            ResourceFailure { Kind: ResourceFailureKind.Forbidden } => 403,
            IdentityFailure { Kind: IdentityFailureKind.Conflict } => 409,
            IdentityFailure { Kind: IdentityFailureKind.Unauthorized } => 401,
            IdentityFailure { Kind: IdentityFailureKind.NotFound } => 404,
            _ => 0
        };
        if (status == 0) return false;
        context.Response.StatusCode = status;
        var error = exception switch
        {
            ReviewTitleException title => new ApiError(title.Kind == ReviewTitleFailureKind.Required
                ? "review_title_required" : "review_title_too_long", title.Message),
            InvalidCatalogFilterException => new ApiError("invalid_filter", "Filtro de catálogo inválido ou inexistente."),
            DatabaseUnavailableException => new ApiError("storage_unavailable", "Persistência temporariamente indisponível."),
            _ => ApiError.FromStatus(status)
        };
        await context.Response.WriteAsJsonAsync(error, cancellationToken);
        return true;
    }
}
