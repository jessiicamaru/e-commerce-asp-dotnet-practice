using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.Fulfilment;

public class PrepareOrderCommandHandler(IOrderRepository orders)
    : IRequestHandler<PrepareOrderCommand, OrderDetailResponse>
{
    private readonly IOrderRepository _orders = orders;

    public Task<OrderDetailResponse> Handle(PrepareOrderCommand request, CancellationToken cancellationToken) =>
        FulfilmentStep.AdvanceAsync(
            _orders, request.OrderId, OrderStatus.Paid, OrderStatus.Preparing, null, cancellationToken);
}
