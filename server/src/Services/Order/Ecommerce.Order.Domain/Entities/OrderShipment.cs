using Ecommerce.Order.Domain.Enums;

namespace Ecommerce.Order.Domain.Entities;

/// <summary>
/// One seller's part of an order - one parcel, one tracking reference (specs/035).
/// </summary>
/// <remarks>
/// <para>
/// An order holding goods from two sellers and the shop has three of these. <see cref="SellerId"/> null
/// is the shop's own part, moved by an administrator, exactly as <c>products.SellerId</c> null is the
/// shop's own product - one model for everybody who ships, not one for the shop and another for sellers.
/// </para>
/// <para>
/// The order's own <c>Status</c> and <c>TrackingReference</c> are a SUMMARY of its parts, rewritten in
/// the same transaction as every part move, and kept in the words an older image can still parse.
/// </para>
/// </remarks>
public class OrderShipment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }

    /// <summary>Whose part this is. Null: the shop's own goods.</summary>
    public Guid? SellerId { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

    /// <summary>Set when the part is shipped, and only then.</summary>
    public string? TrackingReference { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Order? Order { get; set; }
}
