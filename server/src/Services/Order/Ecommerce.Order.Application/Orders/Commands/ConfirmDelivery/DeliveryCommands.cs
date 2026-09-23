using Ecommerce.Shared.Notifications;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Order.Application.Orders.Commands.ConfirmDelivery;

/// <summary>
/// The customer says one parcel of their order arrived (specs/040). No user id - the owner is the token's
/// subject, and someone else's parcel is "not found" (Constitution IV).
/// </summary>
public record ConfirmDeliveryCommand(Guid OrderId, Guid ShipmentId) : IRequest<OrderDetailResponse>;

/// <summary>
/// Every parcel shipped before <paramref name="ShippedBefore"/> that nobody confirmed is taken as delivered
/// (specs/040 US3). Returns how many. Sent by the sweeper; safe to run twice, or on two instances at once.
/// </summary>
public record AutoConfirmDeliveriesCommand(DateTime ShippedBefore) : IRequest<int>;

public class ConfirmDeliveryCommandHandler(IOrderRepository orders, ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier)
    : IRequestHandler<ConfirmDeliveryCommand, OrderDetailResponse>
{
    private readonly INotifier _notifier = notifier;

    private readonly IAuditTrail _audit = audit;

    private readonly IOrderRepository _orders = orders;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<OrderDetailResponse> Handle(ConfirmDeliveryCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var outcome = await _orders.TryConfirmDeliveryAsync(
            request.OrderId, request.ShipmentId, userId, DateTime.UtcNow, cancellationToken,
            async ct =>
            {
                await _audit.RecordAsync(
                    AuditCategory.Order, "ParcelReceived", "Order", request.OrderId.ToString(),
                    "The customer confirmed a parcel arrived",
                    after: new { Parcel = request.ShipmentId, ConfirmedBy = ParcelDelivery.ByCustomer },
                    cancellationToken: ct);
                await OrderNotices.WithFactsAsync(_orders, request.OrderId,
                    facts => OrderNotices.ReceivedAsync(_notifier, facts, request.ShipmentId, ct), ct);
            });

        switch (outcome)
        {
            case DeliveryConfirmOutcome.NotFound:
                throw new NotFoundException(ParcelDelivery.NotFound);
            case DeliveryConfirmOutcome.NotShipped:
                throw new ConflictException(ParcelDelivery.NotShipped);
        }

        var order = await _orders.GetByIdForUserAsync(request.OrderId, userId, cancellationToken)
            ?? throw new NotFoundException(ParcelDelivery.NotFound);
        return OrderMapping.ToDetail(order);
    }
}

public class AutoConfirmDeliveriesCommandHandler(IOrderRepository orders, ILogger<AutoConfirmDeliveriesCommandHandler> logger,
    IAuditTrail audit)
    : IRequestHandler<AutoConfirmDeliveriesCommand, int>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IOrderRepository _orders = orders;
    private readonly ILogger<AutoConfirmDeliveriesCommandHandler> _logger = logger;

    public async Task<int> Handle(AutoConfirmDeliveriesCommand request, CancellationToken cancellationToken)
    {
        var confirmed = await _orders.AutoConfirmDeliveriesAsync(
            request.ShippedBefore, DateTime.UtcNow, cancellationToken,
            (count, ct) => _audit.RecordAsync(
                AuditCategory.System, "DeliveriesAutoConfirmed", "Parcel", null,
                $"Took {count} parcel(s) shipped before {request.ShippedBefore:u} as delivered; nobody had confirmed them",
                after: new { Count = count, ShippedBefore = request.ShippedBefore },
                cancellationToken: ct));

        if (confirmed > 0)
        {
            _logger.LogInformation(
                "Took {Count} parcel(s) shipped before {Cutoff} as delivered; nobody had confirmed them.",
                confirmed, request.ShippedBefore);
        }

        return confirmed;
    }
}
