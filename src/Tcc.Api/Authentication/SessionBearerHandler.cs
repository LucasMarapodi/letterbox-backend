using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Tcc.Application.Identity;

namespace Tcc.Api.Authentication;

public sealed class SessionBearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, SessionAccess sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SessionBearer";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers.Authorization;
        if (headers.Count == 0) return AuthenticateResult.NoResult();
        if (headers.Count != 1 || !AuthenticationHeaderValue.TryParse(headers[0], out var header) ||
            !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(header.Parameter))
            return AuthenticateResult.Fail("Sessão inválida.");
        var session = await sessions.Resolve(header.Parameter, Context.RequestAborted);
        if (session is null) return AuthenticateResult.Fail("Sessão inválida.");
        Context.Features.Set(new AuthenticatedSession(session));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture))], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }
}
