using Ecommerce.Application.ShopApplications;
using Ecommerce.Domain.Constants;
using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

/// <summary>
/// Asking to sell, and deciding (specs/044). Applicants are whoever the token says; deciding is Staff.
/// </summary>
[Route("api/shop-applications")]
[Authorize]
public class ShopApplicationsController : ApiControllerBase
{
    [Authorize(Roles = RoleNames.Customer)]
    [HttpPost]
    public async Task<IActionResult> Apply([FromBody] ApplyForShopCommand command)
    {
        return StatusCode(StatusCodes.Status201Created, await Mediator.Send(command));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        return Ok(await Mediator.Send(new GetMyShopApplicationsQuery()));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetShopApplicationsQuery(status, page, pageSize)));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        return Ok(await Mediator.Send(new ApproveShopApplicationCommand(id)));
    }

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBody body)
    {
        return Ok(await Mediator.Send(new RejectShopApplicationCommand(id, body.Reason)));
    }

    public record RejectBody(string Reason);
}
