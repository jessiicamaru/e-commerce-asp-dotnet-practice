using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Translations;
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
