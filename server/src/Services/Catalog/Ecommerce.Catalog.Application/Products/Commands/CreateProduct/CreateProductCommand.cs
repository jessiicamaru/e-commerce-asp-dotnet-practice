using Ecommerce.Catalog.Application.Products.Common;
using MediatR;

namespace Ecommerce.Catalog.Application.Products.Commands.CreateProduct;

public record CreateProductCommand(
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    string Sku,
    Guid CategoryId
) : IRequest<ProductResponse>;
