using MediatR;

namespace Ecommerce.Cart.Application.Carts.Commands.RemoveLine;

public record RemoveLineCommand(Guid ProductId) : IRequest;
