using Ecommerce.Inventory.Domain.Enums;

namespace Ecommerce.Inventory.Domain.Entities;

/// <summary>
/// One order's claim on one product's stock. Unique per (OrderId, ProductId) — that constraint is
/// the business-level idempotency guard, not a convenience index.
/// </summary>
public class StockReservation
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Held;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SettledAt { get; set; }

    public string? SettlementReason { get; set; }
}
