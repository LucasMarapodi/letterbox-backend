using Microsoft.AspNetCore.Diagnostics;
using Tcc.Api.Contracts;
using Tcc.Application.Identity;
using Tcc.Domain.Identity;

namespace Tcc.Api;

public sealed class IdentityExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            DomainValidationException => 400,
            IdentityFailure { Kind: IdentityFailureKind.Conflict } => 409,
            IdentityFailure { Kind: IdentityFailureKind.Unauthorized } => 401,
            IdentityFailure { Kind: IdentityFailureKind.NotFound } => 404,
            _ => 0
        };
        if (status == 0) return false;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(ApiError.FromStatus(status), cancellationToken);
        return true;
    }
}
