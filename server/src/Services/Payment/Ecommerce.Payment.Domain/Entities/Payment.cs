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

    /// <summary>
    /// What <see cref="Amount"/> is denominated in (specs/022).
    /// </summary>
    /// <remarks>
    /// Null on rows written before this column existed - deliberately, rather than backfilled with
    /// the shop's default. Those rows recorded an amount whose currency nobody stated, and writing
    /// one in now would turn a gap in the record into a claim about it. Reads present null as the
    /// default and say so.
    /// </remarks>
    public string? Currency { get; set; }

    public PaymentStatus Status { get; set; }

    public string? FailureReason { get; set; }

    /// <summary>
    /// What produced this outcome: "Stub", or VNPay's sandbox or gateway (specs/143). It is written into every row on purpose:
    /// a record that does not say where its outcome came from is one somebody will later assume
    /// came from a bank.
    /// </summary>
    public string Provider { get; set; } = StubProvider;

    public const string StubProvider = "Stub";

    /// <summary>VNPay's sandbox, or the simulator speaking its protocol (specs/143): no money moves.</summary>
    public const string VnPaySandboxProvider = "VnPaySandbox";

    /// <summary>VNPay's production gateway, only with <c>VNPAY_LIVE=true</c>.</summary>
    public const string VnPayProvider = "VnPay";

    /// <summary>The gateway's own transaction number (<c>vnp_TransactionNo</c>); null for the stub and every earlier row.</summary>
    public string? ProviderReference { get; set; }

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
