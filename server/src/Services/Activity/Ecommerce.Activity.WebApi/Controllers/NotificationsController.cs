using Ecommerce.Activity.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Activity.WebApi.Controllers;

/// <summary>
/// The caller's own inbox (specs/042). Nothing here names a user: the reader is the token's subject, and
/// someone else's notification is "not found".
/// </summary>
[Authorize]
public class NotificationsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] GetMyNotificationsQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    /// <summary>The bell's number - polled, so it is kept to one count.</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        return Ok(new { count = await Mediator.Send(new GetMyUnreadCountQuery()) });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await Mediator.Send(new MarkNotificationReadCommand(id));
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        return Ok(new { marked = await Mediator.Send(new MarkAllNotificationsReadCommand()) });
    }
}
