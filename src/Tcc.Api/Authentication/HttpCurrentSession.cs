using Tcc.Application.Identity;

namespace Tcc.Api.Authentication;

public sealed record AuthenticatedSession(SessionTicket Session);

public sealed class HttpCurrentSession(IHttpContextAccessor context) : ICurrentSession
{
    public SessionTicket? Session => context.HttpContext?.Features.Get<AuthenticatedSession>()?.Session;
}
