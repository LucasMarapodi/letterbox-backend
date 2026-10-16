using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Tcc.Api.Contracts;
using Tcc.Application.Identity;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(RegisterAccount register, LoginAccount login,
    CurrentAccount current, LogoutAccount logout) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<PublicUserResponse>(201)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(409)]
    public async Task<ActionResult<PublicUserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = PublicUserResponse.From(await register.Execute(request.Name, request.Email, request.Password, cancellationToken));
        return Created($"/api/v1/users/{user.Id}", user);
    }

    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(401)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await login.Execute(request.Email, request.Password, cancellationToken);
        return Ok(new LoginResponse(result.Session.Token, result.Session.ExpiresAt, PublicUserResponse.From(result.Account)));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<PublicUserResponse>(200)]
    [ProducesResponseType<ApiError>(401)]
    public async Task<ActionResult<PublicUserResponse>> Me(CancellationToken cancellationToken) => Ok(PublicUserResponse.From(await current.Execute(cancellationToken)));

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ApiError>(401)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await logout.Execute(cancellationToken);
        return NoContent();
    }
}
