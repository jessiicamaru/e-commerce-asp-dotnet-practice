namespace Ecommerce.Order.Domain.Enums;

/// <summary>
/// Where one seller's part of an order has got to (specs/035). One step at a time, forwards only.
/// </summary>
public enum ShipmentStatus
{
    /// <summary>Nobody has started it. Read as "Paid" once the order is, since that is what it means.</summary>
    Pending = 1,
    Preparing = 2,
    Shipped = 3
}
