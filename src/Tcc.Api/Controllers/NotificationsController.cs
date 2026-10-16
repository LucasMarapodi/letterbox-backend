using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Social;

namespace Tcc.Api.Controllers;

[ApiController, Authorize, Route("api/v1/notifications")]
public sealed class NotificationsController(QueryNotifications notifications) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<NotificationResponse>> Get() =>
        Ok(notifications.Execute().Select(NotificationResponse.From).ToArray());
}
