using Ecommerce.Order.Application.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Order.WebApi.Controllers;

/// <summary>
/// How the shop is doing (specs/047) - administrators only. Amounts are per currency and never added
/// across currencies. A period is <c>from</c> (inclusive) to <c>to</c> (exclusive), the last 30 days
/// unless given.
/// </summary>
[Route("api/orders/insights")]
[Authorize(Roles = "Admin")]
public class InsightsController : ApiControllerBase
{
    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] GetRevenueQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts([FromQuery] GetTopProductsQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [HttpGet("top-buyers")]
    public async Task<IActionResult> TopBuyers([FromQuery] GetTopBuyersQuery query)
    {
        return Ok(await Mediator.Send(query));
    }
}
