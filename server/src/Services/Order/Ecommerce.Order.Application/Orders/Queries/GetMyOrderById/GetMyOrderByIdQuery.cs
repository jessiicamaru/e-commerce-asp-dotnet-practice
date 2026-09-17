using Ecommerce.Order.Application.Orders.Common;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;

/// <summary>
/// One of the caller's own orders, with its line items. The order id is the only input — the owner
/// comes from the token.
/// </summary>
public record GetMyOrderByIdQuery(Guid OrderId) : IRequest<OrderDetailResponse>;
