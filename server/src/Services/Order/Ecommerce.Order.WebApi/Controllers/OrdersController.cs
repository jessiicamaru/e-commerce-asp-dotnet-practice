using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Order.WebApi.Controllers;

[Authorize]
public class OrdersController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitOrderCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }
}
