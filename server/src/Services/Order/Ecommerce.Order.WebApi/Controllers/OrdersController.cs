using Ecommerce.Order.Application.Orders.Commands.Fulfilment;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Queries.GetCheckoutQuote;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrders;
using Ecommerce.Order.Application.Orders.Queries.GetOrdersForFulfilment;
using Ecommerce.Order.Application.Orders.Queries.GetShippingOptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Order.WebApi.Controllers;

[Authorize]
public class OrdersController : ApiControllerBase
{
    /// <summary>
    /// Where to send it and how - the only things a customer decides at checkout. What is bought comes
    /// from the cart, who is buying from the token, and what it costs from Catalog and the delivery
    /// option. Anything else in the body is ignored.
    /// </summary>
    public record CheckoutRequest(Guid? AddressId, string? ShippingOption);

    public record ShipmentRequest(string? TrackingReference);

    /// <summary>Check out the caller's cart to one of their addresses (feature 011).</summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] CheckoutRequest request)
    {
        var result = await Mediator.Send(new SubmitOrderCommand(request.AddressId, request.ShippingOption ?? string.Empty));
        return Ok(result);
    }

    /// <summary>
    /// What checking out would cost now - the same parts, computed by the same code, as the order the
    /// same choices would place (#38). Places nothing.
    /// </summary>
    [HttpGet("quote")]
    public async Task<IActionResult> GetQuote([FromQuery] Guid? addressId, [FromQuery] string? shippingOption)
    {
        return Ok(await Mediator.Send(new GetCheckoutQuoteQuery(addressId, shippingOption ?? string.Empty)));
    }

    /// <summary>The delivery options and what each costs. Public - prices are not a secret.</summary>
    [AllowAnonymous]
    [HttpGet("shipping-options")]
    public async Task<IActionResult> GetShippingOptions()
    {
        return Ok(await Mediator.Send(new GetShippingOptionsQuery()));
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

    // ------------------------------------------------------------------ fulfilment (staff)

    /// <summary>Staff: every customer's orders in one fulfilment status - Paid, Preparing or Shipped.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("fulfilment")]
    public async Task<IActionResult> GetForFulfilment([FromQuery] GetOrdersForFulfilmentQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    /// <summary>Staff: Paid → Preparing. Repeating it is a no-op; from any other state, 409.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/preparing")]
    public async Task<IActionResult> Prepare(Guid id)
    {
        return Ok(await Mediator.Send(new PrepareOrderCommand(id)));
    }

    /// <summary>Staff: Preparing → Shipped, with a tracking reference.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/shipment")]
    public async Task<IActionResult> Ship(Guid id, [FromBody] ShipmentRequest request)
    {
        return Ok(await Mediator.Send(new ShipOrderCommand(id, request.TrackingReference ?? string.Empty)));
    }
}
