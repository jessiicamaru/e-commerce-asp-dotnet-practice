using Ecommerce.Order.Application.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Order.WebApi.Controllers;

/// <summary>
/// How the signed-in seller's shop is doing (specs/068, #111): their own lines on sold orders, per currency.
/// There is no seller id in any request - the token says whose (Constitution IV). A period is whole UTC days,
/// both ends included, the last 30 unless given (specs/055).
/// </summary>
[Route("api/orders/sales/insights")]
[Authorize(Roles = "Seller")]
public class SalesInsightsController : ApiControllerBase
{
    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] GetSellerRevenueQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts([FromQuery] GetSellerTopProductsQuery query)
    {
        return Ok(await Mediator.Send(query));
    }
}
