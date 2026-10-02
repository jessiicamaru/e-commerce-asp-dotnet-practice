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

    /// <summary>
    /// The public vouchers a shopper could use (specs/114) - anonymous, in the request's currency, without limits or
    /// counts. <c>platform</c> and/or <c>sellerId</c> say whose; <c>productId</c> (+ <c>variantId</c>) narrows to one product.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("public")]
    public async Task<IActionResult> Public(
        [FromQuery] bool platform, [FromQuery] List<Guid>? sellerId, [FromQuery] Guid? productId, [FromQuery] List<Guid>? variantId) =>
        Ok(await Mediator.Send(new GetPublicVouchersQuery(platform, sellerId, productId, variantId)));

    /// <summary>The caller's vouchers: the platform's for an administrator, their own for a seller.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12, [FromQuery] string? search = null, [FromQuery] string? state = null) =>
        Ok(await Mediator.Send(new GetMyVouchersQuery(page, pageSize, search, state)));

    /// <summary>
    /// Corrects an active voucher's name, end, limits and minimums (specs/113) - never what it takes off. Not yours is a
    /// 404; disabled, or a total below the uses made, is a 409.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] EditVoucherCommand command) =>
        Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Stops it being used. Not yours is a 404; already disabled is a 409.</summary>
    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id) =>
        Ok(await Mediator.Send(new DisableVoucherCommand(id)));
}
