using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Social;

namespace Tcc.Api.Controllers;

[ApiController, Authorize, Route("api/v1/notifications")]
public sealed class NotificationsController(QueryNotifications notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> Get(CancellationToken cancellationToken) =>
        Ok((await notifications.Execute(cancellationToken)).Select(NotificationResponse.From).ToArray());
}
