using MediatR;

namespace Ecommerce.Cart.Application.Carts.Commands.AddToCart;

/// <summary>Which product, and how many. Deliberately no user id: whose cart comes from the token.</summary>
public record AddToCartCommand(Guid ProductId, int Quantity) : IRequest;
