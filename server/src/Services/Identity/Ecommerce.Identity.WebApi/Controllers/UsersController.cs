using Ecommerce.Application.Users;
using Ecommerce.Domain.Constants;
using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

/// <summary>
/// Staff looking after accounts (specs/043).
/// </summary>
/// <remarks>
/// The class admits Staff; roles and bans narrow to Admin. What a moderator may do to WHOM - never to an
/// administrator, another moderator or themselves - depends on the target and is decided in the handler.
/// </remarks>
[Authorize(Roles = StaffRoles.Staff)]
public class UsersController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        return Ok(await Mediator.Send(new GetUsersQuery(search, page, pageSize)));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:guid}/roles/{role}")]
    public async Task<IActionResult> Grant(Guid id, string role)
    {
        return Ok(await Mediator.Send(new GrantRoleCommand(id, role)));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{id:guid}/roles/{role}")]
    public async Task<IActionResult> Revoke(Guid id, string role)
    {
        return Ok(await Mediator.Send(new RevokeRoleCommand(id, role)));
    }

    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id, [FromBody] LockBody body)
    {
        return Ok(await Mediator.Send(new LockUserCommand(id, body.Days, body.Reason)));
    }

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id)
    {
        return Ok(await Mediator.Send(new UnlockUserCommand(id)));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("{id:guid}/ban")]
    public async Task<IActionResult> Ban(Guid id, [FromBody] BanBody body)
    {
        return Ok(await Mediator.Send(new BanUserCommand(id, body.Reason)));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("{id:guid}/unban")]
    public async Task<IActionResult> LiftBan(Guid id)
    {
        return Ok(await Mediator.Send(new LiftBanCommand(id)));
    }

    public record LockBody(int Days, string Reason);

    public record BanBody(string Reason);
}
