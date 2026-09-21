using Ecommerce.Order.Application.Orders.Common;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.Fulfilment;

/// <summary>Staff: an order being prepared has been sent.</summary>
public record ShipOrderCommand(Guid OrderId, string TrackingReference) : IRequest<OrderDetailResponse>;
