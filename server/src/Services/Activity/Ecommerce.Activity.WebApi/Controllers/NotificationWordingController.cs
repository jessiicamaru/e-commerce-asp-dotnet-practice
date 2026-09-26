using Ecommerce.Activity.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Activity.WebApi.Controllers;

/// <summary>
/// The words of the notifications (specs/078). Reading the current ones is public - the storefront lays them over its
/// own before anybody signs in; changing them is an administrator's, not a moderator's.
/// </summary>
[Route("api/notifications/wording")]
public class NotificationWordingController : ApiControllerBase
{
    /// <summary>Per language, the keys an administrator reworded and what they say now. Cached for a minute.</summary>
    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Current()
    {
        return Ok(await Mediator.Send(new GetNotificationWordingQuery()));
    }

    /// <summary>Every kind with the placeholders it may use, and every reworded key's current version.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<IActionResult> Overview()
    {
        return Ok(await Mediator.Send(new GetNotificationWordingOverviewQuery()));
    }

    /// <summary>One key's saved versions in one language, newest first.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("{key}/{language}/versions")]
    public async Task<IActionResult> Versions(string key, string language)
    {
        return Ok(await Mediator.Send(new GetNotificationWordingVersionsQuery(key, language)));
    }

    /// <summary>Reword a notice: sanitised, placeholders and links checked, a new version. A stale `expectedVersion` is 409.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{key}/{language}")]
    public async Task<IActionResult> Save(string key, string language, [FromBody] SaveBody body)
    {
        return Ok(await Mediator.Send(new SaveNotificationWordingCommand(key, language, body.Text, body.ExpectedVersion)));
    }

    /// <summary>Back to the storefront's own words, as a new version.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{key}/{language}/reset")]
    public async Task<IActionResult> Reset(string key, string language, [FromBody] VersionBody body)
    {
        return Ok(await Mediator.Send(new ResetNotificationWordingCommand(key, language, body.ExpectedVersion)));
    }

    /// <summary>An earlier version's words, as a new version.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{key}/{language}/versions/{version:int}/restore")]
    public async Task<IActionResult> Restore(string key, string language, int version, [FromBody] VersionBody body)
    {
        return Ok(await Mediator.Send(new RestoreNotificationWordingCommand(key, language, version, body.ExpectedVersion)));
    }

    public record SaveBody(string Text, int ExpectedVersion);

    public record VersionBody(int ExpectedVersion);
}
