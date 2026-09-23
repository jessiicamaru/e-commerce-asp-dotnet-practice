using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Catalog.Application.Categories.Commands.DeleteCategory;

/// <summary>
/// Removes a category nothing is filed under (specs/024).
/// </summary>
/// <remarks>
/// <para>
/// The companion to deleting a product, and it exists for the same reason: the test scripts create a
/// category on every run and none of them clean up, so a shop somebody has been testing against ends
/// with 95 categories of which 93 are called things like <c>E2E 17899682755620</c>. They are worse
/// than the junk products were, because a category shows up in the filter a shopper actually uses.
/// </para>
/// <para>
/// <b>A category with products in it is refused, not emptied.</b> Deleting it would either orphan
/// them or delete them, and neither is what somebody tidying up a taxonomy meant to ask for. The
/// foreign key is <c>RESTRICT</c> and would refuse this anyway; checking first is how the caller
/// gets a sentence instead of a constraint violation.
/// </para>
/// </remarks>
public record DeleteCategoryCommand(Guid CategoryId) : IRequest;

public class DeleteCategoryCommandHandler(
    ICategoryRepository categories,
    IProductRepository products,
    IAuditTrail audit) : IRequestHandler<DeleteCategoryCommand>
{
    private readonly IAuditTrail _audit = audit;

    private readonly ICategoryRepository _categories = categories;
    private readonly IProductRepository _products = products;

    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException($"Category with ID '{request.CategoryId}' was not found.");

        var filed = await _products.CountInCategoryAsync(category.Id, cancellationToken);

        if (filed > 0)
        {
            throw new ConflictException(
                $"'{category.Name}' still has {filed} product(s) filed under it. Move or delete them first.");
        }

        _categories.Remove(category);
        await _audit.RecordAsync(
            AuditCategory.Catalog, "CategoryDeleted", "Category", category.Id.ToString(), $"Category \"{category.Name}\" deleted",
            before: new { category.Name, category.Slug, category.Description, category.ParentCategoryId },
            cancellationToken: cancellationToken);
        await _categories.SaveChangesAsync(cancellationToken);
    }
}
