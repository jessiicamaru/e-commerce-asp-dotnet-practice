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
    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> GetByProductId(Guid productId)
    {
        return Ok(await Mediator.Send(new GetStockByProductIdQuery(productId)));
    }

    public record SetStockOnHandRequest(int QuantityOnHand);

    [Authorize(Roles = "Admin")]
    [HttpPut("{productId:guid}")]
    public async Task<IActionResult> SetOnHand(Guid productId, [FromBody] SetStockOnHandRequest request)
    {
        return Ok(await Mediator.Send(new SetStockOnHandCommand(productId, request.QuantityOnHand)));
    }
}
