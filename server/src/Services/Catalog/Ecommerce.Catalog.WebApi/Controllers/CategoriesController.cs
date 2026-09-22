using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Commands.DeleteCategory;
using Ecommerce.Catalog.Application.Categories.Queries.GetCategories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

public class CategoriesController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await Mediator.Send(new GetCategoriesQuery());
        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Removes a category nothing is filed under (specs/024). A category with products in it is
    /// refused with 409 rather than emptied: neither orphaning them nor deleting them is what
    /// somebody tidying a taxonomy asked for.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteCategoryCommand(id));
        return NoContent();
    }
}
