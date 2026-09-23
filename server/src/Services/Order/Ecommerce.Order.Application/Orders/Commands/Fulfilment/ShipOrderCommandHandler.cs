using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using MediatR;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Order.Application.Orders.Commands.Fulfilment;

public class ShipOrderCommandHandler(IOrderRepository orders,
    IAuditTrail audit)
    : IRequestHandler<ShipOrderCommand, OrderDetailResponse>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IOrderRepository _orders = orders;

    public Task<OrderDetailResponse> Handle(ShipOrderCommand request, CancellationToken cancellationToken) =>
        FulfilmentStep.AdvanceAsync(
            _orders, request.OrderId, ShipmentStatus.Preparing, ShipmentStatus.Shipped,
            request.TrackingReference.Trim(), cancellationToken, _audit);
}
