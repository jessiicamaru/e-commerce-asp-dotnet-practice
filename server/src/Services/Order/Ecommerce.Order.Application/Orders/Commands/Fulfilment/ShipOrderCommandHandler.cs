using Ecommerce.Shared.Notifications;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using MediatR;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Email;

namespace Ecommerce.Order.Application.Orders.Commands.Fulfilment;

public class ShipOrderCommandHandler(IOrderRepository orders,
    IAuditTrail audit,
    INotifier notifier,
    IEmailSender email)
    : IRequestHandler<ShipOrderCommand, OrderDetailResponse>
{
    private readonly INotifier _notifier = notifier;

    private readonly IEmailSender _email = email;

    private readonly IAuditTrail _audit = audit;

    private readonly IOrderRepository _orders = orders;

    public Task<OrderDetailResponse> Handle(ShipOrderCommand request, CancellationToken cancellationToken) =>
        FulfilmentStep.AdvanceAsync(
            _orders, request.OrderId, ShipmentStatus.Preparing, ShipmentStatus.Shipped,
            request.TrackingReference.Trim(), cancellationToken, _audit, _notifier, _email);
}
