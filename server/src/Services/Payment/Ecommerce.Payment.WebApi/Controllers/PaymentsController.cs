using Ecommerce.Payment.Application.Payments.Queries.GetPaymentByOrderId;
using Ecommerce.Payment.Application.Payments.Queries.GetPayments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Payment.WebApi.Controllers;

/// <summary>
/// Admin only: a payment record names a user and an amount, so it is not shopper-facing data.
/// </summary>
[Authorize(Roles = "Admin")]
public class PaymentsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetPaymentsQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetByOrderId(Guid orderId)
    {
        return Ok(await Mediator.Send(new GetPaymentByOrderIdQuery(orderId)));
    }
}
