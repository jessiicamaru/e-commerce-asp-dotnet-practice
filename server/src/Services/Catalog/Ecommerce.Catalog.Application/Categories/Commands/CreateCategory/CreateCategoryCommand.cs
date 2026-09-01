using Ecommerce.Catalog.Application.Categories.Common;
using MediatR;

namespace Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(
    string Name,
    string? Description,
    string Slug,
    Guid? ParentCategoryId
) : IRequest<CategoryResponse>;
