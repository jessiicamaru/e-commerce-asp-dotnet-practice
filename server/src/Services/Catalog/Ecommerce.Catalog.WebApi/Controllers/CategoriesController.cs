using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Commands.DeleteCategory;
using Ecommerce.Catalog.Application.Categories.Queries.GetCategories;
using Ecommerce.Catalog.Application.Categories.Translations;
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

    /// <summary>
    /// This category's name and description in one language (specs/026). An upsert, like a
    /// product's.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/translations/{language}")]
    public async Task<IActionResult> SetTranslation(Guid id, string language, [FromBody] CategoryTranslationRequest request)
    {
        return Ok(await Mediator.Send(
            new SetCategoryTranslationCommand(id, language, request.Name, request.Description)));
    }

    /// <summary>Takes a language away; the category falls back to its default text.</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}/translations/{language}")]
    public async Task<IActionResult> RemoveTranslation(Guid id, string language)
    {
        await Mediator.Send(new RemoveCategoryTranslationCommand(id, language));
        return NoContent();
    }

    public record CategoryTranslationRequest(string Name, string? Description);
}
