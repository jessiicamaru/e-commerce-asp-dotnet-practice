using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Audit;

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
}
