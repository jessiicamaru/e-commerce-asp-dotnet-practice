using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using MediatR;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Order.Application.Orders.Commands.Fulfilment;

public class PrepareOrderCommandHandler(IOrderRepository orders,
    IAuditTrail audit)
    : IRequestHandler<PrepareOrderCommand, OrderDetailResponse>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IOrderRepository _orders = orders;

    public Task<OrderDetailResponse> Handle(PrepareOrderCommand request, CancellationToken cancellationToken) =>
        FulfilmentStep.AdvanceAsync(
            _orders, request.OrderId, ShipmentStatus.Pending, ShipmentStatus.Preparing, null, cancellationToken, _audit);
}
