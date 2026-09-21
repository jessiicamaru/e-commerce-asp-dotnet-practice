using Ecommerce.Cart.Application.Common;
using MediatR;

namespace Ecommerce.Cart.Application.Carts.Queries.GetMyCart;

public record GetMyCartQuery : IRequest<CartResponse>;
