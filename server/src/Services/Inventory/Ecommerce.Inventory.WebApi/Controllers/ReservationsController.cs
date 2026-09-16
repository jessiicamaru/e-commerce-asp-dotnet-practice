using Ecommerce.Inventory.Application.Reservations.Queries.GetReservationsByOrderId;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Inventory.WebApi.Controllers;

/// <summary>Lets a stuck order be diagnosed without database access.</summary>
[Authorize(Roles = "Admin")]
public class ReservationsController : ApiControllerBase
{
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetByOrderId(Guid orderId)
    {
        return Ok(await Mediator.Send(new GetReservationsByOrderIdQuery(orderId)));
    }
}
