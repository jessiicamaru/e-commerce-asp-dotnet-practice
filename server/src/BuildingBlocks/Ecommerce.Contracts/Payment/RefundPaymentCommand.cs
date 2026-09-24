namespace Ecommerce.Contracts.Payment;

/// <summary>
/// Give back what was charged for an order the saga has already failed (specs/053): the payment answered
/// after the saga stopped waiting for it, and the stock it was for is back on the shelf.
/// </summary>
/// <remarks>
/// Carries no amount on purpose, like <c>OrderCancelledEvent</c>: Payment refunds what its own payment
/// row says it took. Once per order - <c>refunds.OrderId</c> is unique (specs/039).
/// </remarks>
public record RefundPaymentCommand(
    Guid OrderId,
    string Reason
);
