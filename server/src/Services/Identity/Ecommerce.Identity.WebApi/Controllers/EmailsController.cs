using Ecommerce.Application.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

/// <summary>
/// What became of the emails the shop sent (specs/087, #175): the failed ones with why, and sending one again.
/// Administrators only, like the words (specs/077) - the list names who was written to about what.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/emails")]
public class EmailsController : ApiControllerBase
{
    /// <summary>A page of the emails in one state (<c>Failed</c> by default), newest first. Never their data.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status = "Failed", [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetOutgoingEmailsQuery(status, search, page, pageSize)));
    }

    /// <summary>Puts a failed email back in the queue. 409 for one that is not failed, or that carries an expiring link.</summary>
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id)
    {
        return Ok(await Mediator.Send(new RetryEmailCommand(id)));
    }
}
