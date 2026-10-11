using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Identity;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(RegisterAccount register) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<PublicUserResponse>(201)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(409)]
    public ActionResult<PublicUserResponse> Register(RegisterRequest request)
    {
        var user = PublicUserResponse.From(register.Execute(request.Name, request.Email, request.Password));
        return Created($"/api/v1/users/{user.Id}", user);
    }
}
