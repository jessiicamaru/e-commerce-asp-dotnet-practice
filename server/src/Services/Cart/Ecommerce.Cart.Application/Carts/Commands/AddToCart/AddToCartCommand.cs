using MediatR;

namespace Ecommerce.Cart.Application.Carts.Commands.AddToCart;

/// <summary>Which product, and how many. Deliberately no user id: whose cart comes from the token.</summary>
/// <param name="VariantId">
/// Which shape of the product (specs/020). Null means "the product's only variant", which is what a
/// client built before variants sends, and what is true of every product that existed then.
/// </param>
public record AddToCartCommand(Guid ProductId, int Quantity, Guid? VariantId = null) : IRequest;
