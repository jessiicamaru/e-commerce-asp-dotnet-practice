using Ecommerce.Catalog.WebApi.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Commands.UpdateCategory;
using Ecommerce.Catalog.Application.Categories.Commands.DeleteCategory;
using Ecommerce.Catalog.Application.Categories.Commands.MoveCategory;
using Ecommerce.Catalog.Application.Categories.Queries.GetCategories;
using Ecommerce.Catalog.Application.Categories.Translations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

public class CategoriesController : ApiControllerBase
{
    [AllowAnonymous]
    [OutputCache(PolicyName = CatalogueCache.Policy)]
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
    /// A category's own name and description, in the default language (#195, specs/097). The slug never changes.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryTextRequest request)
    {
        return Ok(await Mediator.Send(new UpdateCategoryCommand(id, request.Name, request.Description)));
    }

    /// <summary>
    /// Puts a category under a department, moves it, or makes it a department (specs/158) - its own endpoint, so a
    /// rename never moves anything.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/parent")]
    public async Task<IActionResult> Move(Guid id, [FromBody] CategoryParentRequest request)
    {
        return Ok(await Mediator.Send(new MoveCategoryCommand(id, request.ParentCategoryId)));
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

    public record CategoryTextRequest(string Name, string? Description);

    /// <summary>Null: the top level, a department.</summary>
    public record CategoryParentRequest(Guid? ParentCategoryId);
}
