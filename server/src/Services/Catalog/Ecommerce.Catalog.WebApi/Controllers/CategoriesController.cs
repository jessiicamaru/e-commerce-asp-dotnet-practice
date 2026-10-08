using Ecommerce.Catalog.WebApi.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Commands.UpdateCategory;
using Ecommerce.Catalog.Application.Categories.Commands.DeleteCategory;
using Ecommerce.Catalog.Application.Categories.Commands.MoveCategory;
using Ecommerce.Catalog.Application.Categories.Queries.GetCategories;
using Ecommerce.Catalog.Application.Categories.Translations;
using Ecommerce.Catalog.Application.Specifications;
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

    // ---- Specifications (#366, specs/159): what a category's products are compared by.

    /// <summary>What applies to products filed under this category - its department's, then its own - for anyone.</summary>
    [AllowAnonymous]
    [OutputCache(PolicyName = CatalogueCache.Policy)]
    [HttpGet("{id:guid}/specifications")]
    public async Task<IActionResult> Specifications(Guid id) =>
        Ok(await Mediator.Send(new GetCategorySpecificationsQuery(id)));

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/specifications")]
    public async Task<IActionResult> CreateSpecification(Guid id, [FromBody] SpecificationRequest request) =>
        Ok(await Mediator.Send(new CreateSpecificationCommand(id, request.Code, request.Name, request.Kind, request.Options)));

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/specifications/{specificationId:guid}")]
    public async Task<IActionResult> RenameSpecification(Guid id, Guid specificationId, [FromBody] SpecificationNameRequest request) =>
        Ok(await Mediator.Send(new RenameSpecificationCommand(id, specificationId, request.Name)));

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/specifications/{specificationId:guid}/translations/{language}")]
    public async Task<IActionResult> TranslateSpecification(
        Guid id, Guid specificationId, string language, [FromBody] SpecificationNameRequest request) =>
        Ok(await Mediator.Send(new TranslateSpecificationCommand(id, specificationId, language, request.Name)));

    /// <summary>Refused with 409 while any product has a value for it.</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}/specifications/{specificationId:guid}")]
    public async Task<IActionResult> DeleteSpecification(Guid id, Guid specificationId)
    {
        await Mediator.Send(new DeleteSpecificationCommand(id, specificationId));
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/specifications/{specificationId:guid}/options")]
    public async Task<IActionResult> AddSpecificationOption(Guid id, Guid specificationId, [FromBody] SpecificationOptionInput request) =>
        Ok(await Mediator.Send(new AddSpecificationOptionCommand(id, specificationId, request.Code, request.Value)));

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/specifications/{specificationId:guid}/options/{optionId:guid}")]
    public async Task<IActionResult> RenameSpecificationOption(
        Guid id, Guid specificationId, Guid optionId, [FromBody] SpecificationValueRequest request) =>
        Ok(await Mediator.Send(new RenameSpecificationOptionCommand(id, specificationId, optionId, request.Value)));

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/specifications/{specificationId:guid}/options/{optionId:guid}/translations/{language}")]
    public async Task<IActionResult> TranslateSpecificationOption(
        Guid id, Guid specificationId, Guid optionId, string language, [FromBody] SpecificationValueRequest request) =>
        Ok(await Mediator.Send(new TranslateSpecificationOptionCommand(id, specificationId, optionId, language, request.Value)));

    /// <summary>Refused with 409 while any product holds it.</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}/specifications/{specificationId:guid}/options/{optionId:guid}")]
    public async Task<IActionResult> DeleteSpecificationOption(Guid id, Guid specificationId, Guid optionId)
    {
        await Mediator.Send(new DeleteSpecificationOptionCommand(id, specificationId, optionId));
        return NoContent();
    }

    public record SpecificationRequest(string Code, string Name, string Kind, List<SpecificationOptionInput>? Options);

    public record SpecificationNameRequest(string Name);

    public record SpecificationValueRequest(string Value);

    public record CategoryTranslationRequest(string Name, string? Description);

    public record CategoryTextRequest(string Name, string? Description);

    /// <summary>Null: the top level, a department.</summary>
    public record CategoryParentRequest(Guid? ParentCategoryId);
}
