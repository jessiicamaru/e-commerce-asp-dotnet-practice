using Ecommerce.Catalog.Application.Common.Interfaces;
using FluentValidation;
using FluentValidation.Results;

namespace Ecommerce.Catalog.Application.Categories;

/// <summary>
/// The shape categories may take (specs/158, #363): <b>departments and their categories, two levels</b>. A category with
/// no parent is a department; a category with a parent sits under a department, and has no subcategories of its own.
/// </summary>
/// <remarks>
/// <para>
/// One method, used by creating and by moving, so the two cannot drift apart. The database's <c>RESTRICT</c> foreign key
/// stays the last word on existence; checking here first is how the caller gets a sentence on <c>ParentCategoryId</c>
/// rather than a constraint violation (as specs/024 did for products in a category).
/// </para>
/// <para>
/// Two levels and no more (research D1): every screen that shows the tree - the storefront's filter, its chips, a
/// breadcrumb, the back office's list - is a list with one indent, and the listing's filter is one subquery. A third
/// level is refused rather than stored and half-shown.
/// </para>
/// </remarks>
public static class CategoryTree
{
    /// <summary>Throws a 400 on <c>ParentCategoryId</c> unless <paramref name="parentId"/> may hold this category.</summary>
    /// <param name="categoryId">The category being placed; null while it is being created.</param>
    public static async Task EnsureMayGoUnderAsync(
        ICategoryRepository categories, Guid? categoryId, string categoryName, Guid? parentId,
        CancellationToken cancellationToken)
    {
        if (parentId is not { } id)
        {
            return;
        }

        if (id == categoryId)
        {
            Refuse("A category cannot be its own department.");
        }

        var parent = await categories.GetByIdAsync(id, cancellationToken);
        if (parent is null)
        {
            Refuse("No such category.");
        }

        if (parent!.ParentCategoryId is not null)
        {
            Refuse($"'{parent.Name}' is itself under a department; categories go two levels deep.");
        }

        if (categoryId is { } own && await categories.CountChildrenAsync(own, cancellationToken) is var children and > 0)
        {
            Refuse($"'{categoryName}' has {children} categor{(children == 1 ? "y" : "ies")} under it, so it stays a department.");
        }
    }

    private static void Refuse(string message) =>
        throw new ValidationException([new ValidationFailure("ParentCategoryId", message)]);
}
