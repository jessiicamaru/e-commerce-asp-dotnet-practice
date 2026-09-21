using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Order.WebApi.Controllers;

[Authorize]
public class OrdersController : ApiControllerBase
{
    /// <summary>
    /// Check out the caller's cart. No body: what is bought comes from the cart, who is buying from
    /// the token, and what it costs from Catalog.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Submit()
    {
        var result = await Mediator.Send(new SubmitOrderCommand());
        return Ok(result);
    }

    /// <summary>
    /// The caller's own orders, newest first. Neither this nor <see cref="GetById"/> accepts a user
    /// id — see <see cref="GetMyOrdersQuery"/>.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] GetMyOrdersQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// One of the caller's own orders. Answers 404 both when the order does not exist and when it
    /// belongs to another shopper.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetMyOrderByIdQuery(id));
        return Ok(result);
    }
}
