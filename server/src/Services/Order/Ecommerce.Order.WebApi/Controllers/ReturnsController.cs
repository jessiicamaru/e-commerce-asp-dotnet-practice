using Ecommerce.Order.Application.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Order.WebApi.Controllers;

/// <summary>
/// Sending a delivered parcel back (specs/066, #107). Under <c>/api/orders</c>, beside the order it belongs to:
/// the buyer's steps on their order, the seller's on their sale, staff's on fulfilment.
/// </summary>
[Authorize]
[Route("api/orders")]
public class ReturnsController : ApiControllerBase
{
    public record ReasonRequest(string? Reason);

    public record SentBackRequest(string? TrackingReference);

    // ------------------------------------------------------------------ the buyer

    /// <summary>Ask to return a whole delivered parcel, within the return window. 404 not theirs; 409 too late, not delivered, or asked already.</summary>
    [HttpPost("{id:guid}/shipments/{shipmentId:guid}/return")]
    public async Task<IActionResult> Request(Guid id, Guid shipmentId, [FromBody] ReasonRequest body) =>
        Ok(await Mediator.Send(new RequestReturnCommand(id, shipmentId, body.Reason ?? string.Empty)));

    /// <summary>Ask staff to look again at a refused return, within the window of the refusal.</summary>
    [HttpPost("{id:guid}/shipments/{shipmentId:guid}/return/escalate")]
    public async Task<IActionResult> Escalate(Guid id, Guid shipmentId) =>
        Ok(await Mediator.Send(new EscalateReturnCommand(id, shipmentId)));

    /// <summary>The accepted parcel is on its way back, with its tracking reference.</summary>
    [HttpPost("{id:guid}/shipments/{shipmentId:guid}/return/sent")]
    public async Task<IActionResult> SentBack(Guid id, Guid shipmentId, [FromBody] SentBackRequest body) =>
        Ok(await Mediator.Send(new SendReturnBackCommand(id, shipmentId, body.TrackingReference ?? string.Empty)));

    // ------------------------------------------------------------------ the seller - their parcel of a sale

    [Authorize(Roles = "Seller")]
    [HttpPost("sales/{id:guid}/return/accept")]
    public async Task<IActionResult> AcceptSale(Guid id) =>
        Ok(await Mediator.Send(new DecideSaleReturnCommand(id, true, null)));

    [Authorize(Roles = "Seller")]
    [HttpPost("sales/{id:guid}/return/refuse")]
    public async Task<IActionResult> RefuseSale(Guid id, [FromBody] ReasonRequest body) =>
        Ok(await Mediator.Send(new DecideSaleReturnCommand(id, false, body.Reason)));

    [Authorize(Roles = "Seller")]
    [HttpPost("sales/{id:guid}/return/received")]
    public async Task<IActionResult> ReceivedSale(Guid id) =>
        Ok(await Mediator.Send(new ReceiveSaleReturnCommand(id)));

    // ------------------------------------------------------------------ staff - the shop's parcels, and escalations

    /// <summary>Returns by state, oldest waiting first - <c>?status=Escalated</c> is the dispute queue.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("returns")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 12) =>
        Ok(await Mediator.Send(new GetReturnsQuery(status, page, pageSize)));

    [Authorize(Roles = "Admin")]
    [HttpPost("fulfilment/{id:guid}/shipments/{shipmentId:guid}/return/accept")]
    public async Task<IActionResult> Accept(Guid id, Guid shipmentId) =>
        Ok(await Mediator.Send(new DecideReturnCommand(id, shipmentId, true, null)));

    [Authorize(Roles = "Admin")]
    [HttpPost("fulfilment/{id:guid}/shipments/{shipmentId:guid}/return/refuse")]
    public async Task<IActionResult> Refuse(Guid id, Guid shipmentId, [FromBody] ReasonRequest body) =>
        Ok(await Mediator.Send(new DecideReturnCommand(id, shipmentId, false, body.Reason)));

    [Authorize(Roles = "Admin")]
    [HttpPost("fulfilment/{id:guid}/shipments/{shipmentId:guid}/return/received")]
    public async Task<IActionResult> Received(Guid id, Guid shipmentId) =>
        Ok(await Mediator.Send(new ReceiveReturnCommand(id, shipmentId)));
}
