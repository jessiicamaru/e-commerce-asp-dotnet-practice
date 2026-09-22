namespace Ecommerce.Contracts.Payment;

/// <param name="Currency">
/// What <paramref name="Amount"/> is denominated in (specs/022). <b>Additive</b>: empty means the
/// shop's default currency, which is what a saga built before this feature sends.
///
/// A payment recorded without one is a record of nothing - <c>Amount 40000005.00</c> with no column
/// saying of what is the defect this feature exists to end, sitting in the one table where money is
/// written down.
/// </param>
public record ProcessPaymentCommand(
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Currency = ""
);

public record PaymentProcessedEvent(
    Guid OrderId,
    Guid PaymentId,
    DateTime ProcessedAt
);

public record PaymentFailedEvent(
    Guid OrderId,
    string Reason
);
