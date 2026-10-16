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
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<PublicUserResponse>> Get(int userId, CancellationToken cancellationToken) => Ok(PublicUserResponse.From(await profiles.Execute(userId, cancellationToken)));

    [Authorize]
    [HttpPatch("me/profile")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<ProfileUpdateResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(401)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<ProfileUpdateResponse>> Patch(ProfilePatchRequest request, CancellationToken cancellationToken) =>
        Ok(ProfileUpdateResponse.From(await edit.Execute(request.ToChange(), request.FavoriteGameIds, cancellationToken)));
}
