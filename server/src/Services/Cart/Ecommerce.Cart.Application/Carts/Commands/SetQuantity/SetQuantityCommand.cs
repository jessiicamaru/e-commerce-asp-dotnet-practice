using MediatR;

namespace Ecommerce.Cart.Application.Carts.Commands.SetQuantity;

/// <summary>Sets a line to exactly this quantity. Zero removes it.</summary>
public record SetQuantityCommand(Guid ProductId, int Quantity) : IRequest;
