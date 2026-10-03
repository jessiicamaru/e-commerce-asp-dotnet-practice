using Ecommerce.Payment.Application.Payments.Checkout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Payment.WebApi.Controllers;

/// <summary>How an order's payment stands, for its owner - and the link to pay at the gateway (specs/143).</summary>
[Route("api/payments/orders")]
[Authorize]
public class PaymentCheckoutsController : ApiControllerBase
{
    [HttpGet("{orderId:guid}/checkout")]
    public async Task<IActionResult> Get(Guid orderId) =>
        Ok(await Mediator.Send(new GetPaymentCheckoutQuery(orderId, ClientAddress(), GatewayLanguage())));

    /// <summary>
    /// The customer's address as the gateway asks for it (<c>vnp_IpAddr</c>): the first hop the gateway in front of this
    /// service recorded, else the connection's. Informational for VNPay, never used to decide anything here.
    /// </summary>
    private string ClientAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].ToString();
        var first = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    /// <summary>VNPay's page in the reader's language: "en", or "vn" for everything else.</summary>
    private string GatewayLanguage() =>
        Request.Headers.AcceptLanguage.ToString().StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "vn";
}
