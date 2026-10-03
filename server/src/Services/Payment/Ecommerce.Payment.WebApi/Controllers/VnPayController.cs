using Ecommerce.Payment.Application.Payments.VnPay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Payment.WebApi.Controllers;

/// <summary>
/// VNPay's server-to-server notification (IPN, specs/143). Anonymous - the gateway carries no token - so the signature
/// is its whole authority, checked before anything else. Always 200: VNPay reads the answer's code, not the status.
/// </summary>
[Route("api/payments/vnpay")]
[AllowAnonymous]
public class VnPayController : ApiControllerBase
{
    [HttpGet("ipn")]
    public async Task<IActionResult> Ipn()
    {
        var query = Request.Query.ToDictionary(p => p.Key, p => p.Value.ToString());
        var answer = await Mediator.Send(new ConfirmVnPayPaymentCommand(query));
        // A dictionary, not an object: the web's camel-casing would write "rspCode", and VNPay reads "RspCode".
        return Ok(new Dictionary<string, string> { ["RspCode"] = answer.RspCode, ["Message"] = answer.Message });
    }
}
