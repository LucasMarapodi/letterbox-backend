using Tcc.Application.Identity;

namespace Tcc.Api.Authentication;

public sealed record AuthenticatedSession(MockSession Session);

public sealed class HttpCurrentSession(IHttpContextAccessor context) : ICurrentSession
{
    public MockSession? Session => context.HttpContext?.Features.Get<AuthenticatedSession>()?.Session;
}
