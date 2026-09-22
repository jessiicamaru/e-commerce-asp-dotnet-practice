using Ecommerce.Application.Sellers;
using Ecommerce.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.WebApi.Controllers;

/// <summary>
/// A seller's own shop (specs/027).
/// </summary>
/// <remarks>
/// <b>Every route here is about the caller and takes no seller id.</b> Who the seller is comes from
/// the token, the way the user id has since specs/009 - an endpoint that accepted an id would let
/// one seller rename another's shop, which is the same defect wearing a third hat.
/// </remarks>
[Authorize(Roles = RoleNames.Seller)]
public class SellersController : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
    {
        return Ok(await Mediator.Send(new GetMyShopQuery()));
    }

    /// <summary>
    /// Renames the caller's shop. <b>No product is written</b> - the catalogue keeps the name as a
    /// read model, so two hundred listings change because one row did.
    /// </summary>
    [HttpPut("me/shop-name")]
    public async Task<IActionResult> Rename([FromBody] RenameShopCommand command)
    {
        return Ok(await Mediator.Send(command));
    }
}
