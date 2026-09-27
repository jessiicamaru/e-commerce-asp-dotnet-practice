using Ecommerce.Catalog.Application.Categories.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Categories.Commands.UpdateCategory;

/// <summary>
/// A category's own name and description - its default-language text - changed by an administrator (#195, specs/097).
/// The slug is never changed: links to the category keep working (spec, Decision). Other languages are translations
/// (specs/026) and are not touched.
/// </summary>
public record UpdateCategoryCommand(Guid Id, string Name, string? Description) : IRequest<CategoryResponse>;

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Category name is required.")
            .MaximumLength(100).WithMessage("Category name must be less than 100 characters.");
        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Category description must be less than 500 characters.");
    }
}

public class UpdateCategoryCommandHandler(ICategoryRepository categories, IAuditTrail audit)
    : IRequestHandler<UpdateCategoryCommand, CategoryResponse>
{
    private readonly ICategoryRepository _categories = categories;
    private readonly IAuditTrail _audit = audit;

    public async Task<CategoryResponse> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Category with ID '{request.Id}' was not found.");

        var before = new { category.Name, category.Description };
        category.Name = request.Name.Trim();
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.UpdatedAt = DateTime.UtcNow;

        await _audit.RecordAsync(
            AuditCategory.Catalog, "CategoryUpdated", "Category", category.Id.ToString(), $"Category \"{category.Name}\" renamed",
            before, new { category.Name, category.Description }, cancellationToken: cancellationToken);
        await _categories.SaveChangesAsync(cancellationToken);

        return CategoryResponse.From(category);
    }
}
