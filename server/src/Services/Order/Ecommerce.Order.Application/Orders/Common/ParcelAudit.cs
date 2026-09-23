using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Notifications;

namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>The audit entry for a parcel moving one step (specs/041) - the shop's, or a seller's.</summary>
public static class ParcelAudit
{
    public static Task RecordAsync(
        IAuditTrail audit, Guid orderId, Guid? sellerId, ShipmentStatus from, ShipmentStatus to, string? tracking,
        CancellationToken cancellationToken) =>
        audit.RecordAsync(
            AuditCategory.Order,
            to == ShipmentStatus.Shipped ? "ParcelShipped" : "ParcelPrepared",
            "Order",
            orderId.ToString(),
            (sellerId is null ? "The shop's parcel" : "A seller's parcel")
                + (to == ShipmentStatus.Shipped ? $" shipped ({tracking})" : " is being prepared"),
            new { Seller = sellerId, Status = OrderMapping.Describe(from) },
            new { Seller = sellerId, Status = OrderMapping.Describe(to), TrackingReference = tracking },
            cancellationToken: cancellationToken);

    /// <summary>The audit entry, and - when the parcel is shipped - the buyer told (specs/042).</summary>
    public static async Task RecordMoveAsync(
        IAuditTrail audit, INotifier? notifier, IOrderRepository orders, Guid orderId, Guid? sellerId,
        ShipmentStatus from, ShipmentStatus to, string? tracking, CancellationToken cancellationToken)
    {
        await RecordAsync(audit, orderId, sellerId, from, to, tracking, cancellationToken);

        if (notifier is not null && to == ShipmentStatus.Shipped)
        {
            await OrderNotices.WithFactsAsync(orders, orderId,
                facts => OrderNotices.ShippedAsync(notifier, facts, sellerId, tracking, cancellationToken), cancellationToken);
        }
    }
}
