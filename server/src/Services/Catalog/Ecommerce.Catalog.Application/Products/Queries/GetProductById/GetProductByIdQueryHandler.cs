using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using MediatR;

namespace Ecommerce.Catalog.Application.Products.Queries.GetProductById;

public class GetProductByIdQueryHandler(IProductRepository productRepository)
    : IRequestHandler<GetProductByIdQuery, ProductResponse?>
{
    private readonly IProductRepository _productRepository = productRepository;

    public async Task<ProductResponse?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
        {
            return null;
        }

        return ProductResponse.From(product);
    }
}
