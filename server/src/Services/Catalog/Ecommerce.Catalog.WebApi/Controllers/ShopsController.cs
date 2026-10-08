using Ecommerce.Catalog.WebApi.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Sellers;
using Ecommerce.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

/// <summary>
/// A shop's page (#197, specs/099) - its products come from <c>GET /api/products?sellerId=</c> - and its state
/// (#214, specs/107): the seller pauses and reopens their own; staff close and reopen any.
/// </summary>
public class ShopsController : ApiControllerBase
{
    [AllowAnonymous]
    [OutputCache(PolicyName = CatalogueCache.Policy)]
    [HttpGet("{sellerId:guid}")]
    public async Task<IActionResult> Get(Guid sellerId) => Ok(await Mediator.Send(new GetShopQuery(sellerId)));

    /// <summary>The caller's own shop; no seller id anywhere in the request.</summary>
    [Authorize(Roles = RoleNames.Seller)]
    [HttpGet("mine")]
    public async Task<IActionResult> Mine() => Ok(await Mediator.Send(new GetMyShopQuery()));

    [Authorize(Roles = RoleNames.Seller)]
    [HttpPost("mine/pause")]
    public async Task<IActionResult> Pause() => Ok(await Mediator.Send(new PauseMyShopCommand()));

    [Authorize(Roles = RoleNames.Seller)]
    [HttpPost("mine/reopen")]
    public async Task<IActionResult> Resume() => Ok(await Mediator.Send(new ResumeMyShopCommand()));

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpGet("closed")]
    public async Task<IActionResult> Closed([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12) =>
        Ok(await Mediator.Send(new GetClosedShopsQuery(pageNumber, pageSize)));

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("{sellerId:guid}/close")]
    public async Task<IActionResult> Close(Guid sellerId, [FromBody] CloseShopRequest body) =>
        Ok(await Mediator.Send(new CloseShopCommand(sellerId, body.Reason)));

    [Authorize(Roles = StaffRoles.Staff)]
    [HttpPost("{sellerId:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid sellerId) => Ok(await Mediator.Send(new ReopenShopCommand(sellerId)));
}

/// <summary>Why staff close a shop; the seller reads it.</summary>
public record CloseShopRequest(string Reason);
