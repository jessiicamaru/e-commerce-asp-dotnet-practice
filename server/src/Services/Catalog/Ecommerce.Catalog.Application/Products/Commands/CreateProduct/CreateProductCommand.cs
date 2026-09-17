using Ecommerce.Catalog.Application.Products.Common;
using MediatR;

namespace Ecommerce.Catalog.Application.Products.Commands.CreateProduct;

/// <summary>
/// Creates a product in the catalogue.
/// </summary>
/// <remarks>
/// <b>Stock quantity is deliberately absent.</b> It used to be here, was stored on the product row,
/// and was never written again — the catalogue accepting a number it does not own and cannot
/// maintain (issue #4). Stock is set through Inventory:
/// <c>PUT http://localhost:5060/api/stock/{productId}</c>.
/// <para>
/// That makes creating a stocked product two steps. The second step was always the only one that
/// did anything; it is now visibly required rather than silently optional. The rejected alternative
/// was carrying an opening quantity on <c>ProductCreatedEvent</c>, which would have meant the
/// catalogue telling the stock owner what its stock is.
/// </para>
/// </remarks>
public record CreateProductCommand(
    string Name,
    string? Description,
    decimal Price,
    string Sku,
    Guid CategoryId
) : IRequest<ProductResponse>;
