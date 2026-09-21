using Ecommerce.Order.Application.Orders.Common;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.Fulfilment;

/// <summary>Staff: a paid order is being prepared.</summary>
public record PrepareOrderCommand(Guid OrderId) : IRequest<OrderDetailResponse>;
