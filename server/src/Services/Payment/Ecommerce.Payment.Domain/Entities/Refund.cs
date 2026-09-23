namespace Ecommerce.Payment.Domain.Entities;

/// <summary>
/// What was given back for a cancelled order (specs/039) - the whole of what was charged.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>The provider is a stub, so no money moved.</b> Like the payment it reverses, this row says
/// <see cref="Provider"/> "Stub": it is the ledger entry a real refund would be recorded as, not a refund.
/// </para>
/// <para>
/// A table of its own rather than a status on <see cref="Payment"/>: a payment row is immutable once
/// written, <c>payments.OrderId</c> is unique, and a new <c>PaymentStatus</c> value is one an image rolled
/// back to before this could not parse (research D3). Unique on <see cref="OrderId"/> too - one refund per
/// order, however often the cancellation is delivered.
/// </para>
/// </remarks>
public class Refund
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string Provider { get; set; } = "Stub";
    public DateTime RefundedAt { get; set; } = DateTime.UtcNow;
}
