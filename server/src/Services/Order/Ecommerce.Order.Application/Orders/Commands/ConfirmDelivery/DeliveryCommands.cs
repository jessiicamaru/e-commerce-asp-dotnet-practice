using Ecommerce.Shared.Notifications;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MassTransit;
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
    INotifier notifier,
    IPublishEndpoint publish)
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
                await ParcelDeliveries.AnnounceAsync(_orders, publish, [request.ShipmentId], ct);
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
    IAuditTrail audit,
    IPublishEndpoint publish)
    : IRequestHandler<AutoConfirmDeliveriesCommand, int>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IOrderRepository _orders = orders;
    private readonly ILogger<AutoConfirmDeliveriesCommandHandler> _logger = logger;

    public async Task<int> Handle(AutoConfirmDeliveriesCommand request, CancellationToken cancellationToken)
    {
        var confirmed = await _orders.AutoConfirmDeliveriesAsync(
            request.ShippedBefore, DateTime.UtcNow, cancellationToken,
            async (parcels, ct) =>
            {
                await _audit.RecordAsync(
                    AuditCategory.System, "DeliveriesAutoConfirmed", "Parcel", null,
                    $"Took {parcels.Count} parcel(s) shipped before {request.ShippedBefore:u} as delivered; nobody had confirmed them",
                    after: new { Count = parcels.Count, ShippedBefore = request.ShippedBefore },
                    cancellationToken: ct);
                await ParcelDeliveries.AnnounceAsync(_orders, publish, parcels, ct);
            });

        if (confirmed > 0)
        {
            _logger.LogInformation(
                "Took {Count} parcel(s) shipped before {Cutoff} as delivered; nobody had confirmed them.",
                confirmed, request.ShippedBefore);
        }

        return confirmed;
    }
}

/// <summary>
/// Tells the rest of the system what a customer received (specs/046) - one event per parcel, staged in the
/// transaction that marked it delivered, so it is announced exactly when it happened and never twice.
/// </summary>
public static class ParcelDeliveries
{
    public static async Task AnnounceAsync(
        IOrderRepository orders, IPublishEndpoint publish, IReadOnlyCollection<Guid> shipmentIds, CancellationToken cancellationToken)
    {
        foreach (var parcel in await orders.GetDeliveredParcelsAsync(shipmentIds, cancellationToken))
        {
            await publish.Publish(new Ecommerce.Contracts.Order.ParcelDeliveredEvent(
                parcel.OrderId, parcel.ShipmentId, parcel.BuyerId, parcel.ProductIds, parcel.DeliveredAt), cancellationToken);
        }
    }
}
