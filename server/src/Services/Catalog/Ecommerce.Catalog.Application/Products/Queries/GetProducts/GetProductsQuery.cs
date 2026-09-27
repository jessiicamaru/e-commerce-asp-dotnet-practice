using Ecommerce.Catalog.Application.Common.Models;
using Ecommerce.Catalog.Application.Products.Common;
using MediatR;

namespace Ecommerce.Catalog.Application.Products.Queries.GetProducts;

public record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    string? SearchTerm = null,
    string? SortBy = null,
    /// <summary>One shop's products (#197, specs/099) - on the shelf only, like the rest of the listing.</summary>
    Guid? SellerId = null
) : IRequest<PaginatedList<ProductResponse>>;
