using Ecommerce.Application.Sellers;
using Ecommerce.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

/// <summary>
/// Sellers' payout accounts in full, for the administrator who transfers to them (specs/106) - the one route that
/// returns a whole account number. Its own controller, because <c>SellersController</c> is <c>Seller</c> at class level
/// and ASP.NET requires every <c>Authorize</c> on the way: an administrator who does not sell would be refused.
/// </summary>
[Route("api/sellers/payout-accounts")]
[Authorize(Roles = RoleNames.Admin)]
public class PayoutAccountsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? sellerIds)
    {
        var ids = (sellerIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        return Ok(await Mediator.Send(new GetPayoutAccountsQuery(ids)));
    }
}
