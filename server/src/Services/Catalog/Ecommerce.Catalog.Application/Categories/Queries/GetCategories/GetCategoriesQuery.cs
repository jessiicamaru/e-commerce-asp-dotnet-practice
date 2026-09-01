using Ecommerce.Catalog.Application.Categories.Common;
using MediatR;

namespace Ecommerce.Catalog.Application.Categories.Queries.GetCategories;

public record GetCategoriesQuery : IRequest<List<CategoryResponse>>;
