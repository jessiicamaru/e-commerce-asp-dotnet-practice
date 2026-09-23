using Ecommerce.Order.Application.Orders.Commands.Fulfilment;
using Ecommerce.Order.Application.Orders.Commands.RecordPayout;
using Ecommerce.Order.Application.Orders.Commands.SellerFulfilment;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Queries.GetCheckoutQuote;
using Ecommerce.Order.Application.Orders.Queries.GetMyBalance;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrders;
using Ecommerce.Order.Application.Orders.Queries.GetMyPayouts;
using Ecommerce.Order.Application.Orders.Queries.GetMySale;
using Ecommerce.Order.Application.Orders.Queries.GetMySales;
using Ecommerce.Order.Application.Orders.Queries.GetOrdersForFulfilment;
using Ecommerce.Order.Application.Orders.Queries.GetPayoutsDue;
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

    /// <summary>Whom to settle, in which currency. No amount - see <see cref="RecordPayoutCommand"/>.</summary>
    public record PayoutRequest(Guid SellerId, string? Currency);

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

    // ------------------------------------------------------------------ sales (sellers, specs/034)

    /// <summary>
    /// A seller's sales: paid orders holding at least one of their lines, with figures over those lines
    /// only. <b>Seller, not Admin</b> - an administrator sees every order through fulfilment already.
    /// </summary>
    [Authorize(Roles = "Seller")]
    [HttpGet("sales")]
    public async Task<IActionResult> GetMySales([FromQuery] GetMySalesQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    /// <summary>
    /// One sale, the seller's own lines only. 404 - one wording - for no such order, nothing of theirs
    /// on it, failed, or still settling.
    /// </summary>
    [Authorize(Roles = "Seller")]
    [HttpGet("sales/{id:guid}")]
    public async Task<IActionResult> GetMySale(Guid id)
    {
        return Ok(await Mediator.Send(new GetMySaleQuery(id)));
    }

    /// <summary>
    /// A seller starts preparing THEIR part of this order (specs/035). 404 - one wording - when it is
    /// not their sale, not there, not paid or failed; 409 when their part is not waiting.
    /// </summary>
    [Authorize(Roles = "Seller")]
    [HttpPost("sales/{id:guid}/preparing")]
    public async Task<IActionResult> PrepareMySale(Guid id)
    {
        return Ok(await Mediator.Send(new PrepareMySaleCommand(id)));
    }

    /// <summary>A seller has sent THEIR part, with a tracking reference. Repeating it is a no-op.</summary>
    [Authorize(Roles = "Seller")]
    [HttpPost("sales/{id:guid}/shipment")]
    public async Task<IActionResult> ShipMySale(Guid id, [FromBody] ShipmentRequest request)
    {
        return Ok(await Mediator.Send(new ShipMySaleCommand(id, request.TrackingReference ?? string.Empty)));
    }

    // ------------------------------------------------------------------ money (specs/037)

    /// <summary>A seller's money per currency: on the way, due, paid out.</summary>
    [Authorize(Roles = "Seller")]
    [HttpGet("sales/balance")]
    public async Task<IActionResult> GetMyBalance()
    {
        return Ok(await Mediator.Send(new GetMyBalanceQuery()));
    }

    /// <summary>The payouts made to a seller, newest first.</summary>
    [Authorize(Roles = "Seller")]
    [HttpGet("sales/payouts")]
    public async Task<IActionResult> GetMyPayouts([FromQuery] GetMyPayoutsQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    /// <summary>Staff: every seller with something due now, per currency.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("payouts/due")]
    public async Task<IActionResult> GetPayoutsDue()
    {
        return Ok(await Mediator.Send(new GetPayoutsDueQuery()));
    }

    /// <summary>
    /// Staff: settle everything due to one seller in one currency. 201 with the payout; 409 when nothing
    /// is due - including when another administrator has just settled it.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("payouts")]
    public async Task<IActionResult> RecordPayout([FromBody] PayoutRequest request)
    {
        var payout = await Mediator.Send(new RecordPayoutCommand(request.SellerId, request.Currency ?? string.Empty));
        return StatusCode(StatusCodes.Status201Created, payout);
    }

    // ------------------------------------------------------------------ fulfilment (staff)
    // Since specs/035 these move the SHOP's part of the order - its own goods - and nothing a seller sold.

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
