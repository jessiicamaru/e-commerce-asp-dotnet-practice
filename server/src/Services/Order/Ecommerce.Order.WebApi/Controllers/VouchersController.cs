using Ecommerce.Order.Application.Vouchers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Order.WebApi.Controllers;

/// <summary>
/// Vouchers (specs/069, #108). An administrator makes and manages the platform's; a seller their own shop's. Whose
/// a voucher is comes from the token - there is no owner in any request. A customer uses one by its code at
/// checkout (<c>/api/orders/quote</c> and <c>POST /api/orders</c>), never here.
/// </summary>
[Route("api/vouchers")]
[Authorize(Roles = "Admin,Seller")]
public class VouchersController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVoucherCommand command) =>
        Ok(await Mediator.Send(command));

    /// <summary>The caller's vouchers: the platform's for an administrator, their own for a seller.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 12) =>
        Ok(await Mediator.Send(new GetMyVouchersQuery(page, pageSize)));

    /// <summary>Stops it being used. Not yours is a 404; already disabled is a 409.</summary>
    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id) =>
        Ok(await Mediator.Send(new DisableVoucherCommand(id)));
}
