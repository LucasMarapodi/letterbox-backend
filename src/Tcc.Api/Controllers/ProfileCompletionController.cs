using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Identity;

namespace Tcc.Api.Controllers;

[ApiController, Authorize, Route("api/v1/users/me/profile-completion")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileCompletionController(ProfileCompletion completion) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProfileCompletionDetails>(200)]
    [ProducesResponseType<ApiError>(401)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<ProfileCompletionDetails>> Get(CancellationToken cancellationToken) => Ok(await completion.Get(cancellationToken));

    [HttpPut]
    [ProducesResponseType<ProfileCompletionDetails>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<ApiError>(401)]
    [ProducesResponseType<ApiError>(404)]
    public async Task<ActionResult<ProfileCompletionDetails>> Put(ProfileCompletionRequest request, CancellationToken cancellationToken) =>
        Ok(await completion.Set(request.Country, request.State, request.Bio, request.AvatarSpecified, request.AvatarUrl, request.FavoriteGameIds, cancellationToken));
}
