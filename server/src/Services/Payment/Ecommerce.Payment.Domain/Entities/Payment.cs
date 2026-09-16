using Ecommerce.Payment.Domain.Enums;

namespace Ecommerce.Payment.Domain.Entities;

/// <summary>
/// One attempt to charge for one order. Immutable once written: a redelivered request resolves to
/// the row already there rather than changing it, which is what makes replay safe without a
/// guarded update.
/// </summary>
public class Payment
{
    /// <summary>Also the PaymentId reported back to the saga, so a trace leads to a real record.</summary>
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; }

    public string? FailureReason { get; set; }

    /// <summary>
    /// What produced this outcome. Always "Stub" today. It is written into every row on purpose:
    /// a record that does not say where its outcome came from is one somebody will later assume
    /// came from a bank.
    /// </summary>
    public string Provider { get; set; } = StubProvider;

    public const string StubProvider = "Stub";

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
