using Ecommerce.Catalog.Application.Products.Saved;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

/// <summary>
/// Products a shopper saved for later (specs/075, #109). Every call is about the caller's own list: whose it is
/// comes from the token (Constitution IV), so there is no shopper id to change into somebody else's.
/// </summary>
[Route("api/products")]
[Authorize]
public class SavedProductsController : ApiControllerBase
{
    /// <summary>Save it. Again is fine; a product not on sale is a 404, like its public page.</summary>
    [HttpPut("{id:guid}/saved")]
    public async Task<IActionResult> Save(Guid id)
    {
        await Mediator.Send(new SaveProductCommand(id));
        return NoContent();
    }

    /// <summary>Unsave it. Something not saved is no error.</summary>
    [HttpDelete("{id:guid}/saved")]
    public async Task<IActionResult> Unsave(Guid id)
    {
        await Mediator.Send(new UnsaveProductCommand(id));
        return NoContent();
    }

    /// <summary>The caller's saved products, newest first, as the listing reads them now.</summary>
    [HttpGet("saved")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 12) =>
        Ok(await Mediator.Send(new GetSavedProductsQuery(page, pageSize)));

    /// <summary>The ids alone - for drawing a filled heart on a page of cards.</summary>
    [HttpGet("saved/ids")]
    public async Task<IActionResult> Ids() => Ok(await Mediator.Send(new GetSavedProductIdsQuery()));
}
