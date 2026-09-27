using Ecommerce.Activity.Application.Audit.Queries;
using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Activity.WebApi.Controllers;

/// <summary>
/// One person's moderation history, for the staff deciding about them (#198, specs/100). Moderation entries only - the
/// rest of the audit log stays an administrator's.
/// </summary>
[Route("api/audit/people")]
[Authorize(Roles = StaffRoles.Staff)]
public class PersonHistoryController : ApiControllerBase
{
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await Mediator.Send(new GetPersonModerationHistoryQuery(userId, page, pageSize)));
}
