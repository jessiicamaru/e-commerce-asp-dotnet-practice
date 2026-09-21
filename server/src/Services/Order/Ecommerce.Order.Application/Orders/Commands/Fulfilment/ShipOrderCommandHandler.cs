using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.Fulfilment;

public class ShipOrderCommandHandler(IOrderRepository orders)
    : IRequestHandler<ShipOrderCommand, OrderDetailResponse>
{
    private readonly IOrderRepository _orders = orders;

    public Task<OrderDetailResponse> Handle(ShipOrderCommand request, CancellationToken cancellationToken) =>
        FulfilmentStep.AdvanceAsync(
            _orders, request.OrderId, OrderStatus.Preparing, OrderStatus.Shipped,
            request.TrackingReference.Trim(), cancellationToken);
}
