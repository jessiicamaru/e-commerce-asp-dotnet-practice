using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Common.Models;
using Ecommerce.Catalog.Application.Products.Common;
using MediatR;

namespace Ecommerce.Catalog.Application.Products.Queries.GetProducts;

public class GetProductsQueryHandler(IProductRepository productRepository)
    : IRequestHandler<GetProductsQuery, PaginatedList<ProductResponse>>
{
    private readonly IProductRepository _productRepository = productRepository;

    public async Task<PaginatedList<ProductResponse>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _productRepository.GetPaginatedAsync(
            request.PageNumber,
            request.PageSize,
            request.CategoryId,
            request.SearchTerm,
            request.SortBy,
            cancellationToken
        );

        var dtos = items.Select(ProductResponse.From).ToList();

        return new PaginatedList<ProductResponse>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
