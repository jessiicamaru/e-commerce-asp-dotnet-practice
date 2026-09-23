namespace Ecommerce.Order.Domain.Entities;

/// <summary>
/// A record that the shop settled what it owed one seller in one currency (specs/037). Payment is a
/// stub, so nothing was transferred by this system: this is the ledger entry, not the transfer.
/// </summary>
/// <remarks>
/// The parts it covers point at it through <see cref="OrderShipment.PayoutId"/>, set by the one guarded
/// statement that claims them - which is what stops a part being paid twice.
/// </remarks>
public class Payout
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public string Currency { get; set; } = string.Empty;

    /// <summary>Σ (goods − commission + delivery share) over the parts it claimed.</summary>
    public decimal Amount { get; set; }

    public int PartCount { get; set; }

    /// <summary>The administrator who recorded it, from their token.</summary>
    public Guid RecordedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
