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

    /// <summary>
    /// The return this refunds, for a parcel sent back (specs/066) - null for a whole order (a cancellation, a
    /// late payment). An order has at most one refund of the whole, and one per return.
    /// </summary>
    public Guid? ReturnId { get; set; }
    public DateTime RefundedAt { get; set; } = DateTime.UtcNow;
}
