using Ecommerce.Order.Domain.Enums;

namespace Ecommerce.Order.Domain.Entities;

/// <summary>
/// A buyer's request to send a delivered parcel back (specs/066, #107) - one per parcel, whole parcels only.
/// Moved only by guarded statements from the state each step expects; see <see cref="ReturnStatus"/>.
/// </summary>
public class ParcelReturn
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    /// <summary>The parcel. Unique: a parcel is returned once or not at all.</summary>
    public Guid ShipmentId { get; set; }

    /// <summary>The buyer, from their token when they asked.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Whose parcel it is - null for the shop's own, which an administrator answers.</summary>
    public Guid? SellerId { get; set; }

    public ReturnStatus Status { get; set; }

    /// <summary>Why the buyer wants to return it.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Why it was refused or rejected - the buyer reads it.</summary>
    public string? DecisionReason { get; set; }

    /// <summary>How the buyer sent it back.</summary>
    public string? TrackingReference { get; set; }

    public DateTime RequestedAt { get; set; }

    /// <summary>When it was last accepted, refused or rejected - the window to escalate or send back counts from here.</summary>
    public DateTime? DecidedAt { get; set; }

    public DateTime? SentBackAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    /// <summary>Goods plus their tax, in the order's currency - what the buyer is refunded once it is received.</summary>
    public decimal? RefundAmount { get; set; }

    public DateTime UpdatedAt { get; set; }

    public OrderShipment? Shipment { get; set; }
}
