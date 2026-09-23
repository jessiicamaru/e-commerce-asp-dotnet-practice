using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Exceptions;

namespace Ecommerce.Order.Application.Orders.Commands.Fulfilment;

/// <summary>
/// One guarded fulfilment step on the SHOP's part of an order, and what each outcome means to staff.
/// </summary>
/// <remarks>
/// Since specs/035 an order is fulfilled in parts, one per seller plus the shop's own. These staff
/// endpoints keep their addresses and move the shop's part - so an order of the shop's goods alone,
/// which is one part, behaves exactly as it always did. A seller's part is the seller's to move.
/// </remarks>
internal static class FulfilmentStep
{
    public static async Task<OrderDetailResponse> AdvanceAsync(
        IOrderRepository orders,
        Guid orderId,
        ShipmentStatus from,
        ShipmentStatus to,
        string? trackingReference,
        CancellationToken cancellationToken)
    {
        var result = await orders.TryMoveShipmentAsync(
            orderId, sellerId: null, from, to, trackingReference, DateTime.UtcNow, cancellationToken);

        switch (result.Outcome)
        {
            case ShipmentMoveOutcome.Moved:
            case ShipmentMoveOutcome.AlreadyThere: // staff double-click, retried request (FR-016)
                var order = await orders.GetByIdAsync(orderId, cancellationToken)
                    ?? throw new NotFoundException("Order not found.");
                return OrderMapping.ToDetail(order);

            case ShipmentMoveOutcome.OrderNotPaid:
                throw new ConflictException(
                    $"Order is {OrderMapping.Describe(result.OrderStatus!.Value)}; only a Paid order can be fulfilled.");

            case ShipmentMoveOutcome.NoSuchPart when result.OrderStatus is null:
                throw new NotFoundException("Order not found.");

            case ShipmentMoveOutcome.NoSuchPart:
                throw new ConflictException(
                    "This order has no part the shop ships; each seller ships their own.");

            default: // WrongState
                if (result.Current == to)
                {
                    throw new ConflictException(
                        $"The shop's part is already {to} with tracking reference '{result.CurrentTracking}'.");
                }

                throw new ConflictException(
                    $"The shop's part is {Describe(result.Current)}; only a {Describe(from)} part can become {to}.");
        }
    }

    /// <summary>A part nobody has started is, to anyone reading it, part of a paid order.</summary>
    public static string Describe(ShipmentStatus? status) =>
        status is null or ShipmentStatus.Pending ? nameof(OrderStatus.Paid) : status.Value.ToString();
}
