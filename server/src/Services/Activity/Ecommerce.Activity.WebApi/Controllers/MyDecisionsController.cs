using Ecommerce.Activity.Application.Audit.Queries;
using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Activity.WebApi.Controllers;

/// <summary>
/// What the caller decided as staff, newest first (specs/045) - the moderator's "recently" on their
/// dashboard. Moderation entries they are the actor of, and nothing else: the rest of the audit log stays
/// an administrator's.
/// </summary>
[Route("api/audit/mine")]
[Authorize(Roles = StaffRoles.Staff)]
public class MyDecisionsController(ICurrentUser currentUser) : ApiControllerBase
{
    private readonly ICurrentUser _currentUser = currentUser;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        var me = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        return Ok(await Mediator.Send(new GetAuditEntriesQuery(Category: "Moderation", ActorId: me, Page: page, PageSize: pageSize)));
    }
}
