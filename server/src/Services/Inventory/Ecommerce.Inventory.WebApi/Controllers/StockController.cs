using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using Ecommerce.Inventory.Application.Stock.Queries.GetStock;
using Ecommerce.Inventory.Application.Stock.Queries.GetStockByProductId;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Inventory.WebApi.Controllers;

public class StockController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetStockQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [AllowAnonymous]
    /// <summary>
    /// Stock for one sellable unit. Since specs/020 the id is a <b>variant</b> id - what a customer
    /// actually buys. For every product that existed before variants, its id is also its only
    /// variant's id, so an old link still works.
    /// </summary>
    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> GetByProductId(Guid productId)
    {
        return Ok(await Mediator.Send(new GetStockByProductIdQuery(productId)));
    }

    public record SetStockOnHandRequest(int QuantityOnHand);

    [Authorize(Roles = "Admin")]
    /// <summary>Sets stock for one sellable unit; the id is a <b>variant</b> id (specs/020).</summary>
    [HttpPut("{productId:guid}")]
    public async Task<IActionResult> SetOnHand(Guid productId, [FromBody] SetStockOnHandRequest request)
    {
        return Ok(await Mediator.Send(new SetStockOnHandCommand(productId, request.QuantityOnHand)));
    }
}
