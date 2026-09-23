using Ecommerce.Activity.Application.Audit.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Activity.WebApi.Controllers;

/// <summary>
/// The audit log (specs/041). ⚠️ Administrators only: it names who did what to whom, and a moderator
/// reading who locked them would be reading their own file.
/// </summary>
[Authorize(Roles = "Admin")]
public class AuditController : ApiControllerBase
{
    /// <summary>A page of entries, newest first, filtered by category, action, actor, subject and period.</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetAuditEntriesQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    /// <summary>How many entries each category holds in a period.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] GetAuditSummaryQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    /// <summary>One entry, with its snapshots and field-level diff.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await Mediator.Send(new GetAuditEntryQuery(id)));
    }
}
