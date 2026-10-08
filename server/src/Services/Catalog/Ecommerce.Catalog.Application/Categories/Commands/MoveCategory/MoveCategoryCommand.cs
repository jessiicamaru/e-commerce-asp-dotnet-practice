using Ecommerce.Catalog.Application.Categories.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Catalog.Application.Categories.Commands.MoveCategory;

/// <summary>
/// Puts a category under a department, moves it to another, or makes it a department of its own (specs/158, #363).
/// </summary>
/// <remarks>
/// Its own command and its own endpoint (research D2): <c>PUT /api/categories/{id}</c> takes the name and description,
/// and adding the parent to it would make every existing caller send null and lift the category out of its department.
/// The name, the slug and the translations do not change.
/// </remarks>
public record MoveCategoryCommand(Guid Id, Guid? ParentCategoryId) : IRequest<CategoryResponse>;

public class MoveCategoryCommandHandler(ICategoryRepository categories, IAuditTrail audit)
    : IRequestHandler<MoveCategoryCommand, CategoryResponse>
{
    private readonly ICategoryRepository _categories = categories;
    private readonly IAuditTrail _audit = audit;

    public async Task<CategoryResponse> Handle(MoveCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Category with ID '{request.Id}' was not found.");

        if (category.ParentCategoryId == request.ParentCategoryId)
        {
            return CategoryResponse.From(category);
        }

        await CategoryTree.EnsureMayGoUnderAsync(
            _categories, category.Id, category.Name, request.ParentCategoryId, cancellationToken);

        var before = new { category.ParentCategoryId };
        category.ParentCategoryId = request.ParentCategoryId;
        category.UpdatedAt = DateTime.UtcNow;

        await _audit.RecordAsync(
            AuditCategory.Catalog, "CategoryMoved", "Category", category.Id.ToString(),
            request.ParentCategoryId is null
                ? $"Category \"{category.Name}\" made a department"
                : $"Category \"{category.Name}\" moved under another department",
            before, new { category.ParentCategoryId }, cancellationToken: cancellationToken);
        await _categories.SaveChangesAsync(cancellationToken);

        return CategoryResponse.From(category);
    }
}
