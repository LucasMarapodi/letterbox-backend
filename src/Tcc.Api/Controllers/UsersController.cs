using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Identity;

namespace Tcc.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(GetUserProfile profiles, EditUserProfile edit) : ControllerBase
{
    [HttpGet("{userId}")]
    [ProducesResponseType<PublicUserResponse>(200)]
    [ProducesResponseType<ApiError>(404)]
    public ActionResult<PublicUserResponse> Get(string userId) => Ok(PublicUserResponse.From(profiles.Execute(userId)));

    [Authorize]
    [HttpPatch("me/profile")]
    [ProducesResponseType<PublicUserResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(401)]
    public ActionResult<PublicUserResponse> Patch(ProfilePatchRequest request) =>
        Ok(PublicUserResponse.From(edit.Execute(request.ToChange())));
}
