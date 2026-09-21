using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Exceptions;

namespace Ecommerce.Order.Application.Orders.Commands.Fulfilment;

/// <summary>
/// One guarded fulfilment step, and what a zero-row result means.
/// </summary>
internal static class FulfilmentStep
{
    public static async Task<OrderDetailResponse> AdvanceAsync(
        IOrderRepository orders,
        Guid orderId,
        OrderStatus from,
        OrderStatus to,
        string? trackingReference,
        CancellationToken cancellationToken)
    {
        var rows = await orders.TryAdvanceAsync(orderId, from, to, trackingReference, DateTime.UtcNow, cancellationToken);

        var order = await orders.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");

        if (rows == 1)
        {
            return OrderMapping.ToDetail(order);
        }

        // Zero rows. Asking again for the step that was just taken is a no-op, not an error - staff
        // double-click, requests are retried (FR-016). Anything else was refused and changed nothing.
        var isRepeat = order.Status == to
            && (trackingReference is null || order.TrackingReference == trackingReference);

        if (isRepeat)
        {
            return OrderMapping.ToDetail(order);
        }

        if (order.Status == to)
        {
            throw new ConflictException(
                $"Order is already {to} with tracking reference '{order.TrackingReference}'.");
        }

        throw new ConflictException(
            $"Order is {OrderMapping.Describe(order.Status)}; only a {from} order can become {to}.");
    }
}
