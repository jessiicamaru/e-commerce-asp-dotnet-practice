namespace Ecommerce.Catalog.Application.Categories.Common;

public record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    string Slug,
    Guid? ParentCategoryId,
    bool IsActive
);
