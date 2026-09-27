using Ecommerce.Catalog.Application.Sellers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

/// <summary>A shop's page (#197, specs/099). Its products come from <c>GET /api/products?sellerId=</c>.</summary>
public class ShopsController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("{sellerId:guid}")]
    public async Task<IActionResult> Get(Guid sellerId) => Ok(await Mediator.Send(new GetShopQuery(sellerId)));
}
